using System.Collections.Concurrent;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Metrics;

namespace MDRAVA.API.Proxy.Resilience;

public sealed class CircuitBreakerStore
{
    private readonly ConcurrentDictionary<string, MutableCircuitState> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly ProxyMetrics _metrics;
    private readonly TimeProvider _timeProvider;

    public CircuitBreakerStore(ProxyMetrics metrics, TimeProvider timeProvider)
    {
        _metrics = metrics;
        _timeProvider = timeProvider;
    }

    public bool IsAvailable(RuntimeUpstream upstream)
    {
        if (!upstream.CircuitBreaker.Enabled)
        {
            return true;
        }

        var state = GetOrCreate(upstream);
        lock (state.Gate)
        {
            RefreshOpenState(upstream, state, _timeProvider.GetUtcNow());
            return state.State switch
            {
                CircuitBreakerRuntimeState.Open => false,
                CircuitBreakerRuntimeState.HalfOpen => state.HalfOpenInFlight < upstream.CircuitBreaker.HalfOpenMaxAttempts,
                _ => true
            };
        }
    }

    public void RecordRejectedIfUnavailable(RuntimeUpstream upstream)
    {
        if (!upstream.CircuitBreaker.Enabled)
        {
            return;
        }

        var state = GetOrCreate(upstream);
        lock (state.Gate)
        {
            RefreshOpenState(upstream, state, _timeProvider.GetUtcNow());
            if (state.State == CircuitBreakerRuntimeState.Open
                || (state.State == CircuitBreakerRuntimeState.HalfOpen
                    && state.HalfOpenInFlight >= upstream.CircuitBreaker.HalfOpenMaxAttempts))
            {
                state.RejectedRequests++;
                _metrics.CircuitRejected(upstream);
            }
        }
    }

    public bool TryAcquire(RuntimeUpstream upstream, out CircuitBreakerLease? lease)
    {
        lease = null;
        if (!upstream.CircuitBreaker.Enabled)
        {
            lease = new CircuitBreakerLease(upstream, enabled: false, halfOpenProbe: false, _ => { });
            return true;
        }

        var state = GetOrCreate(upstream);
        lock (state.Gate)
        {
            RefreshOpenState(upstream, state, _timeProvider.GetUtcNow());
            if (state.State == CircuitBreakerRuntimeState.Open)
            {
                state.RejectedRequests++;
                _metrics.CircuitRejected(upstream);
                return false;
            }

            var halfOpenProbe = state.State == CircuitBreakerRuntimeState.HalfOpen;
            if (halfOpenProbe)
            {
                if (state.HalfOpenInFlight >= upstream.CircuitBreaker.HalfOpenMaxAttempts)
                {
                    state.RejectedRequests++;
                    _metrics.CircuitRejected(upstream);
                    return false;
                }

                state.HalfOpenInFlight++;
            }

            lease = new CircuitBreakerLease(upstream, enabled: true, halfOpenProbe, ReleaseHalfOpenProbe);
            return true;
        }
    }

    public void RecordSuccess(CircuitBreakerLease? lease)
    {
        if (lease is null || !lease.Enabled || !lease.TryComplete())
        {
            return;
        }

        var state = GetOrCreate(lease.Upstream);
        lock (state.Gate)
        {
            if (lease.HalfOpenProbe)
            {
                state.HalfOpenInFlight = Math.Max(0, state.HalfOpenInFlight - 1);
                Close(lease.Upstream, state);
                return;
            }

            state.FailureCount = 0;
            state.WindowStartedAtUtc = null;
            state.LastFailureReason = null;
        }
    }

    public void RecordFailure(CircuitBreakerLease? lease, string reason, int? statusCode = null)
    {
        if (lease is null || !lease.Enabled || !lease.TryComplete())
        {
            return;
        }

        if (statusCode.HasValue && !ContainsStatus(lease.Upstream.CircuitBreaker.FailureStatusCodes, statusCode.Value))
        {
            ReleaseHalfOpenProbe(lease);
            return;
        }

        var state = GetOrCreate(lease.Upstream);
        var now = _timeProvider.GetUtcNow();
        lock (state.Gate)
        {
            state.LastFailureReason = NormalizeReason(reason);
            if (lease.HalfOpenProbe)
            {
                state.HalfOpenInFlight = Math.Max(0, state.HalfOpenInFlight - 1);
                Open(lease.Upstream, state, now);
                return;
            }

            if (state.WindowStartedAtUtc is null || now - state.WindowStartedAtUtc > lease.Upstream.CircuitBreaker.SamplingWindow)
            {
                state.WindowStartedAtUtc = now;
                state.FailureCount = 0;
            }

            state.FailureCount++;
            if (state.FailureCount >= lease.Upstream.CircuitBreaker.FailureThreshold)
            {
                Open(lease.Upstream, state, now);
            }
        }
    }

    public CircuitBreakerStatus Snapshot(RuntimeUpstream upstream)
    {
        if (!upstream.CircuitBreaker.Enabled)
        {
            return new CircuitBreakerStatus(
                CircuitBreakerRuntimeState.Disabled,
                false,
                upstream.CircuitBreaker.FailureThreshold,
                upstream.CircuitBreaker.HalfOpenMaxAttempts,
                null,
                null,
                0,
                0,
                null);
        }

        var state = GetOrCreate(upstream);
        lock (state.Gate)
        {
            RefreshOpenState(upstream, state, _timeProvider.GetUtcNow());
            return new CircuitBreakerStatus(
                state.State,
                true,
                upstream.CircuitBreaker.FailureThreshold,
                upstream.CircuitBreaker.HalfOpenMaxAttempts,
                state.OpenedAtUtc,
                state.State == CircuitBreakerRuntimeState.Open && state.OpenedAtUtc.HasValue
                    ? state.OpenedAtUtc.Value.Add(upstream.CircuitBreaker.OpenDuration)
                    : null,
                state.FailureCount,
                state.RejectedRequests,
                state.LastFailureReason);
        }
    }

    private void RefreshOpenState(RuntimeUpstream upstream, MutableCircuitState state, DateTimeOffset now)
    {
        if (state.State != CircuitBreakerRuntimeState.Open || state.OpenedAtUtc is null)
        {
            return;
        }

        if (now - state.OpenedAtUtc.Value >= upstream.CircuitBreaker.OpenDuration)
        {
            state.State = CircuitBreakerRuntimeState.HalfOpen;
            state.HalfOpenInFlight = 0;
            _metrics.CircuitHalfOpened(upstream);
        }
    }

    private void Open(RuntimeUpstream upstream, MutableCircuitState state, DateTimeOffset now)
    {
        state.State = CircuitBreakerRuntimeState.Open;
        state.OpenedAtUtc = now;
        state.HalfOpenInFlight = 0;
        state.WindowStartedAtUtc = now;
        state.FailureCount = 0;
        _metrics.CircuitOpened(upstream);
    }

    private void Close(RuntimeUpstream upstream, MutableCircuitState state)
    {
        state.State = CircuitBreakerRuntimeState.Closed;
        state.OpenedAtUtc = null;
        state.WindowStartedAtUtc = null;
        state.FailureCount = 0;
        state.HalfOpenInFlight = 0;
        state.LastFailureReason = null;
        _metrics.CircuitClosed(upstream);
    }

    private void ReleaseHalfOpenProbe(CircuitBreakerLease lease)
    {
        if (!lease.Enabled || !lease.HalfOpenProbe)
        {
            return;
        }

        var state = GetOrCreate(lease.Upstream);
        lock (state.Gate)
        {
            state.HalfOpenInFlight = Math.Max(0, state.HalfOpenInFlight - 1);
        }
    }

    private MutableCircuitState GetOrCreate(RuntimeUpstream upstream)
    {
        return _states.GetOrAdd(upstream.Identity, _ => new MutableCircuitState());
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
