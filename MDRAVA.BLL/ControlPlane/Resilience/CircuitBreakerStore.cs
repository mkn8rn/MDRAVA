using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Resilience;

public sealed class CircuitBreakerStore
{
    private readonly ConcurrentDictionary<string, MutableCircuitState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly IProxyCircuitBreakerMetricsSink _metrics;
    private readonly TimeProvider _timeProvider;

    public CircuitBreakerStore(IProxyCircuitBreakerMetricsSink metrics, TimeProvider timeProvider)
    {
        _metrics = metrics;
        _timeProvider = timeProvider;
    }

    public bool IsAvailable(CircuitBreakerStatusSource source)
    {
        if (!source.Policy.Enabled)
        {
            return true;
        }

        var state = GetOrCreate(source.UpstreamIdentity);
        lock (state.Gate)
        {
            RefreshOpenState(source.Policy, state, _timeProvider.GetUtcNow());
            return state.State switch
            {
                CircuitBreakerRuntimeState.Open => false,
                CircuitBreakerRuntimeState.HalfOpen => state.HalfOpenInFlight < source.Policy.HalfOpenMaxAttempts,
                _ => true
            };
        }
    }

    public void RecordRejectedIfUnavailable(CircuitBreakerStatusSource source)
    {
        if (!source.Policy.Enabled)
        {
            return;
        }

        var state = GetOrCreate(source.UpstreamIdentity);
        lock (state.Gate)
        {
            RefreshOpenState(source.Policy, state, _timeProvider.GetUtcNow());
            if (state.State == CircuitBreakerRuntimeState.Open
                || (state.State == CircuitBreakerRuntimeState.HalfOpen
                    && state.HalfOpenInFlight >= source.Policy.HalfOpenMaxAttempts))
            {
                state.RejectedRequests++;
                _metrics.CircuitRejected();
            }
        }
    }

    public bool TryAcquire(CircuitBreakerStatusSource source, [MaybeNullWhen(false)] out CircuitBreakerLease lease)
    {
        if (!source.Policy.Enabled)
        {
            lease = new CircuitBreakerLease(source, enabled: false, halfOpenProbe: false, _ => { });
            return true;
        }

        var state = GetOrCreate(source.UpstreamIdentity);
        lock (state.Gate)
        {
            RefreshOpenState(source.Policy, state, _timeProvider.GetUtcNow());
            if (state.State == CircuitBreakerRuntimeState.Open)
            {
                lease = null;
                state.RejectedRequests++;
                _metrics.CircuitRejected();
                return false;
            }

            var halfOpenProbe = state.State == CircuitBreakerRuntimeState.HalfOpen;
            if (halfOpenProbe)
            {
                if (state.HalfOpenInFlight >= source.Policy.HalfOpenMaxAttempts)
                {
                    lease = null;
                    state.RejectedRequests++;
                    _metrics.CircuitRejected();
                    return false;
                }

                state.HalfOpenInFlight++;
            }

            lease = new CircuitBreakerLease(source, enabled: true, halfOpenProbe, ReleaseHalfOpenProbe);
            return true;
        }
    }

    public void RecordSuccess(CircuitBreakerLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);

        if (!lease.Enabled || !lease.TryComplete())
        {
            return;
        }

        var state = GetOrCreate(lease.Source.UpstreamIdentity);
        lock (state.Gate)
        {
            if (lease.HalfOpenProbe)
            {
                state.HalfOpenInFlight = Math.Max(0, state.HalfOpenInFlight - 1);
                Close(state);
                return;
            }

            state.FailureCount = 0;
            state.WindowStartedAtUtc = null;
            state.LastFailureReason = null;
        }
    }

    public void RecordFailure(CircuitBreakerLease lease, string reason, int? statusCode = null)
    {
        ArgumentNullException.ThrowIfNull(lease);

        if (!lease.Enabled || !lease.TryComplete())
        {
            return;
        }

        if (statusCode.HasValue && !ContainsStatus(lease.Source.Policy.FailureStatusCodes, statusCode.Value))
        {
            ReleaseHalfOpenProbe(lease);
            return;
        }

        var state = GetOrCreate(lease.Source.UpstreamIdentity);
        var now = _timeProvider.GetUtcNow();
        lock (state.Gate)
        {
            state.LastFailureReason = NormalizeReason(reason);
            if (lease.HalfOpenProbe)
            {
                state.HalfOpenInFlight = Math.Max(0, state.HalfOpenInFlight - 1);
                Open(state, now);
                return;
            }

            if (state.WindowStartedAtUtc is null || now - state.WindowStartedAtUtc > lease.Source.Policy.SamplingWindow)
            {
                state.WindowStartedAtUtc = now;
                state.FailureCount = 0;
            }

            state.FailureCount++;
            if (state.FailureCount >= lease.Source.Policy.FailureThreshold)
            {
                Open(state, now);
            }
        }
    }

    public CircuitBreakerStatus Snapshot(CircuitBreakerStatusSource source)
    {
        if (!source.Policy.Enabled)
        {
            return CircuitBreakerStatus.Disabled(source.Policy);
        }

        var state = GetOrCreate(source.UpstreamIdentity);
        lock (state.Gate)
        {
            RefreshOpenState(source.Policy, state, _timeProvider.GetUtcNow());
            return CircuitBreakerStatus.FromEnabledPolicyState(
                source.Policy,
                state.State,
                state.OpenedAtUtc,
                state.FailureCount,
                state.RejectedRequests,
                state.LastFailureReason);
        }
    }

    private void RefreshOpenState(RuntimeCircuitBreakerPolicy policy, MutableCircuitState state, DateTimeOffset now)
    {
        if (state.State != CircuitBreakerRuntimeState.Open || state.OpenedAtUtc is null)
        {
            return;
        }

        if (now - state.OpenedAtUtc.Value >= policy.OpenDuration)
        {
            state.State = CircuitBreakerRuntimeState.HalfOpen;
            state.HalfOpenInFlight = 0;
            _metrics.CircuitHalfOpened();
        }
    }

    private void Open(MutableCircuitState state, DateTimeOffset now)
    {
        state.State = CircuitBreakerRuntimeState.Open;
        state.OpenedAtUtc = now;
        state.HalfOpenInFlight = 0;
        state.WindowStartedAtUtc = now;
        state.FailureCount = 0;
        _metrics.CircuitOpened();
    }

    private void Close(MutableCircuitState state)
    {
        state.State = CircuitBreakerRuntimeState.Closed;
        state.OpenedAtUtc = null;
        state.WindowStartedAtUtc = null;
        state.FailureCount = 0;
        state.HalfOpenInFlight = 0;
        state.LastFailureReason = null;
        _metrics.CircuitClosed();
    }

    private void ReleaseHalfOpenProbe(CircuitBreakerLease lease)
    {
        if (!lease.Enabled || !lease.HalfOpenProbe)
        {
            return;
        }

        var state = GetOrCreate(lease.Source.UpstreamIdentity);
        lock (state.Gate)
        {
            state.HalfOpenInFlight = Math.Max(0, state.HalfOpenInFlight - 1);
        }
    }

    private MutableCircuitState GetOrCreate(string upstreamIdentity)
    {
        return _states.GetOrAdd(upstreamIdentity, _ => new MutableCircuitState());
    }

    private static bool ContainsStatus(IReadOnlyList<int> statusCodes, int statusCode)
    {
        return statusCodes.Any(code => code == statusCode);
    }

    private static string NormalizeReason(string reason)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? "unknown"
            : reason.Trim().ToLowerInvariant().Replace(' ', '_');
    }

    private sealed class MutableCircuitState
    {
        public object Gate { get; } = new();

        public CircuitBreakerRuntimeState State { get; set; } = CircuitBreakerRuntimeState.Closed;

        public DateTimeOffset? WindowStartedAtUtc { get; set; }

        public DateTimeOffset? OpenedAtUtc { get; set; }

        public int FailureCount { get; set; }

        public int HalfOpenInFlight { get; set; }

        public long RejectedRequests { get; set; }

        public string? LastFailureReason { get; set; }
    }
}
