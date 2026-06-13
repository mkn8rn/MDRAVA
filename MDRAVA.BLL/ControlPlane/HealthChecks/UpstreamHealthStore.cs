using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Status;
using System.Collections.Concurrent;

namespace MDRAVA.BLL.ControlPlane.HealthChecks;

public sealed class UpstreamHealthStore : IProxyStatusUpstreamHealthSource
{
    private readonly ConcurrentDictionary<string, MutableUpstreamHealth> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly IProxyUpstreamHealthMetricsSink _metrics;
    private readonly IUpstreamConnectionPruner _connectionPruner;
    private readonly CircuitBreakerStore _circuitBreakerStore;

    public UpstreamHealthStore(
        IProxyUpstreamHealthMetricsSink metrics,
        IUpstreamConnectionPruner connectionPruner,
        CircuitBreakerStore circuitBreakerStore)
    {
        _metrics = metrics;
        _connectionPruner = connectionPruner;
        _circuitBreakerStore = circuitBreakerStore;
    }

    public bool IsUsable(UpstreamHealthStateSource source)
    {
        return !_states.TryGetValue(source.UpstreamIdentity, out var state)
            || state.State != UpstreamHealthState.Unhealthy;
    }

    public void RecordSelection(UpstreamHealthStateSource source)
    {
        var state = GetOrCreate(source);
        state.RecordSelection();
    }

    public void RecordRequestFailure(UpstreamHealthStateSource source)
    {
        var state = GetOrCreate(source);
        state.RecordRequestFailure();
        _metrics.UpstreamRequestFailed();
    }

    public UpstreamHealthState RecordHealthCheckResult(
        UpstreamHealthCheckTarget target,
        HealthCheckSample sample,
        DateTimeOffset checkedAtUtc)
    {
        var state = GetOrCreate(
            target.UpstreamIdentity,
            target.RouteName,
            target.UpstreamName,
            target.UpstreamEndpoint);
        lock (state.Gate)
        {
            var previous = state.RecordHealthCheckSample(
                sample,
                checkedAtUtc,
                target.HealthyThreshold,
                target.UnhealthyThreshold);

            if (state.State != previous)
            {
                _metrics.UpstreamHealthTransition();
                if (state.State == UpstreamHealthState.Unhealthy)
                {
                    _connectionPruner.PruneIdleConnections(target.TransportEndpoint);
                }
            }

            return state.State;
        }
    }

    public IReadOnlyList<ProxyUpstreamStatus> Snapshot(
        IReadOnlyList<ProxyUpstreamHealthSource> upstreams)
    {
        if (upstreams.Count == 0)
        {
            return [];
        }

        List<ProxyUpstreamStatus> records = [];
        foreach (var source in upstreams)
        {
            var state = GetOrCreate(source.HealthState);
            lock (state.Gate)
            {
                state.RecordHealthCheckConfiguration(source.HealthCheckEnabled);
                records.Add(new ProxyUpstreamStatus(
                    source.HealthState.RouteName,
                    source.HealthState.UpstreamName,
                    source.HealthState.UpstreamEndpoint,
                    source.Scheme,
                    source.ValidateCertificate,
                    source.EffectiveSniHost,
                    source.HealthCheckEnabled,
                    state.State,
                    state.LastResult,
                    state.LastCheckedAtUtc,
                    state.ConsecutiveSuccesses,
                    state.ConsecutiveFailures,
                    state.ReadSelectedRequests(),
                    state.ReadRequestFailures())
                {
                    Protocol = source.Protocol,
                    Weight = source.Weight,
                    CircuitBreaker = _circuitBreakerStore.Snapshot(source.CircuitBreaker)
                });
            }
        }

        return records;
    }

    public IReadOnlyList<ProxyUpstreamStatus> ReadUpstreams(
        IReadOnlyList<ProxyUpstreamHealthSource> upstreams)
    {
        return Snapshot(upstreams);
    }

    private MutableUpstreamHealth GetOrCreate(UpstreamHealthStateSource source)
    {
        return GetOrCreate(
            source.UpstreamIdentity,
            source.RouteName,
            source.UpstreamName,
            source.UpstreamEndpoint);
    }

    private MutableUpstreamHealth GetOrCreate(
        string upstreamIdentity,
        string routeName,
        string upstreamName,
        string endpoint)
    {
        return _states.GetOrAdd(
            upstreamIdentity,
            _ => new MutableUpstreamHealth(routeName, upstreamName, endpoint));
    }

    private sealed class MutableUpstreamHealth
    {
        public MutableUpstreamHealth(string routeName, string upstreamName, string endpoint)
        {
            RouteName = routeName;
            UpstreamName = upstreamName;
            Endpoint = endpoint;
        }

        public object Gate { get; } = new();

        public string RouteName { get; }

        public string UpstreamName { get; }

        public string Endpoint { get; }

        public bool HealthCheckEnabled { get; private set; }

        public UpstreamHealthState State { get; private set; } = UpstreamHealthState.Unknown;

        public string? LastResult { get; private set; }

        public DateTimeOffset? LastCheckedAtUtc { get; private set; }

        public int ConsecutiveSuccesses { get; private set; }

        public int ConsecutiveFailures { get; private set; }

        private long _selectedRequests;

        private long _requestFailures;

        public void RecordHealthCheckConfiguration(bool enabled)
        {
            HealthCheckEnabled = enabled;
        }

        public void RecordSelection()
        {
            Interlocked.Increment(ref _selectedRequests);
        }

        public void RecordRequestFailure()
        {
            Interlocked.Increment(ref _requestFailures);
        }

        public long ReadSelectedRequests()
        {
            return Interlocked.Read(ref _selectedRequests);
        }

        public long ReadRequestFailures()
        {
            return Interlocked.Read(ref _requestFailures);
        }

        public UpstreamHealthState RecordHealthCheckSample(
            HealthCheckSample sample,
            DateTimeOffset checkedAtUtc,
            int healthyThreshold,
            int unhealthyThreshold)
        {
            RecordHealthCheckConfiguration(enabled: true);
            LastResult = sample.Result;
            LastCheckedAtUtc = checkedAtUtc;
            var previous = State;

            if (sample.Healthy)
            {
                ConsecutiveSuccesses++;
                ConsecutiveFailures = 0;
                if (State != UpstreamHealthState.Healthy
                    && ConsecutiveSuccesses >= healthyThreshold)
                {
                    State = UpstreamHealthState.Healthy;
                }
            }
            else
            {
                ConsecutiveFailures++;
                ConsecutiveSuccesses = 0;
                if (State != UpstreamHealthState.Unhealthy
                    && ConsecutiveFailures >= unhealthyThreshold)
                {
                    State = UpstreamHealthState.Unhealthy;
                }
            }

            return previous;
        }
    }
}
