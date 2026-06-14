using System.Collections.Concurrent;

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
                state.RecordRejectedRequest();
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
                state.RecordRejectedRequest();
                _metrics.CircuitRejected();
                return CircuitBreakerAcquisitionResult.Rejected;
            }

            var halfOpenProbe = state.State == CircuitBreakerRuntimeState.HalfOpen;
            if (halfOpenProbe)
            {
                if (state.HalfOpenInFlight >= source.Policy.HalfOpenMaxAttempts)
                {
                    state.RecordRejectedRequest();
                    _metrics.CircuitRejected();
                    return CircuitBreakerAcquisitionResult.Rejected;
                }

                state.RecordHalfOpenProbeStarted();
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
                state.RecordHalfOpenProbeCompleted();
                Close(state);
                return;
            }

            state.ClearFailureTracking();
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
            var normalizedReason = NormalizeReason(reason);
            if (lease.HalfOpenProbe)
            {
                state.RecordFailureReason(normalizedReason);
                state.RecordHalfOpenProbeCompleted();
                Open(state, now);
                return;
            }

            var failureCount = state.RecordFailure(
                normalizedReason,
                now,
                lease.Source.Policy.SamplingWindow);
            if (failureCount >= lease.Source.Policy.FailureThreshold)
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
