using System.Collections.Concurrent;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Resilience;

public sealed partial class CircuitBreakerStore
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

    public CircuitBreakerAcquisitionResult Acquire(CircuitBreakerStatusSource source)
    {
        if (!source.Policy.Enabled)
        {
            return CircuitBreakerAcquisitionResult.Accepted(
                new CircuitBreakerLease(source, enabled: false, halfOpenProbe: false, _ => { }));
        }

        var state = GetOrCreate(source.UpstreamIdentity);
        lock (state.Gate)
        {
            RefreshOpenState(source.Policy, state, _timeProvider.GetUtcNow());
            if (state.State == CircuitBreakerRuntimeState.Open)
            {
                state.RejectedRequests++;
                _metrics.CircuitRejected();
                return CircuitBreakerAcquisitionResult.Rejected;
            }

            var halfOpenProbe = state.State == CircuitBreakerRuntimeState.HalfOpen;
            if (halfOpenProbe)
            {
                if (state.HalfOpenInFlight >= source.Policy.HalfOpenMaxAttempts)
                {
                    state.RejectedRequests++;
                    _metrics.CircuitRejected();
                    return CircuitBreakerAcquisitionResult.Rejected;
                }

                state.HalfOpenInFlight++;
            }

            return CircuitBreakerAcquisitionResult.Accepted(
                new CircuitBreakerLease(source, enabled: true, halfOpenProbe, ReleaseHalfOpenProbe));
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

    private MutableCircuitState GetOrCreate(string upstreamIdentity)
    {
        return _states.GetOrAdd(upstreamIdentity, _ => new MutableCircuitState());
    }
}

public abstract record CircuitBreakerAcquisitionResult
{
    private CircuitBreakerAcquisitionResult()
    {
    }

    public static CircuitBreakerAcquisitionResult Rejected { get; } = new RejectedResult();

    public static CircuitBreakerAcquisitionResult Accepted(CircuitBreakerLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return new AcceptedResult(lease);
    }

    public sealed record AcceptedResult : CircuitBreakerAcquisitionResult
    {
        public AcceptedResult(CircuitBreakerLease lease)
        {
            ArgumentNullException.ThrowIfNull(lease);
            Lease = lease;
        }

        public CircuitBreakerLease Lease { get; }
    }

    public sealed record RejectedResult : CircuitBreakerAcquisitionResult;
}
