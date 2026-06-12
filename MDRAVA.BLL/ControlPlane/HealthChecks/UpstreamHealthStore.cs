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
        Interlocked.Increment(ref state.SelectedRequests);
    }

    public void RecordRequestFailure(UpstreamHealthStateSource source)
    {
        var state = GetOrCreate(source);
        Interlocked.Increment(ref state.RequestFailures);
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
            state.HealthCheckEnabled = true;
            state.LastResult = sample.Result;
            state.LastCheckedAtUtc = checkedAtUtc;
            var previous = state.State;

            if (sample.Healthy)
            {
                state.ConsecutiveSuccesses++;
                state.ConsecutiveFailures = 0;
                if (state.State != UpstreamHealthState.Healthy
                    && state.ConsecutiveSuccesses >= target.HealthyThreshold)
                {
                    state.State = UpstreamHealthState.Healthy;
                }
            }
            else
            {
                state.ConsecutiveFailures++;
                state.ConsecutiveSuccesses = 0;
                if (state.State != UpstreamHealthState.Unhealthy
                    && state.ConsecutiveFailures >= target.UnhealthyThreshold)
                {
                    state.State = UpstreamHealthState.Unhealthy;
                }
            }

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

    public IReadOnlyList<ProxyUpstreamStatusResponse> Snapshot(
        IReadOnlyList<ProxyUpstreamHealthSource> upstreams)
    {
        if (upstreams.Count == 0)
        {
            return [];
        }

        List<ProxyUpstreamStatusResponse> records = [];
        foreach (var source in upstreams)
        {
            var state = GetOrCreate(source.HealthState);
            lock (state.Gate)
            {
                state.HealthCheckEnabled = source.HealthCheckEnabled;
                records.Add(new ProxyUpstreamStatusResponse(
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
                    Interlocked.Read(ref state.SelectedRequests),
                    Interlocked.Read(ref state.RequestFailures))
                {
                    Protocol = source.Protocol,
                    Weight = source.Weight,
                    CircuitBreaker = _circuitBreakerStore.Snapshot(source.CircuitBreaker)
                });
            }
        }

        return records;
    }

    public IReadOnlyList<ProxyUpstreamStatusResponse> ReadUpstreams(
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

        public bool HealthCheckEnabled { get; set; }

        public UpstreamHealthState State { get; set; } = UpstreamHealthState.Unknown;

        public string? LastResult { get; set; }

        public DateTimeOffset? LastCheckedAtUtc { get; set; }

        public int ConsecutiveSuccesses { get; set; }

        public int ConsecutiveFailures { get; set; }

        public long SelectedRequests;

        public long RequestFailures;
    }
}
