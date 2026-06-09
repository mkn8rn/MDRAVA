using System.Collections.Concurrent;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane;

public sealed class UpstreamHealthStore : IProxyStatusUpstreamHealthSource
{
    private readonly ConcurrentDictionary<string, MutableUpstreamHealth> _states = new(StringComparer.OrdinalIgnoreCase);
    private readonly IProxyUpstreamHealthMetricsSink _metrics;
    private readonly IUpstreamConnectionPruner _connectionPruner;
    private readonly CircuitBreakerStore? _circuitBreakerStore;

    public UpstreamHealthStore(
        IProxyUpstreamHealthMetricsSink metrics,
        IUpstreamConnectionPruner connectionPruner,
        CircuitBreakerStore? circuitBreakerStore)
    {
        _metrics = metrics;
        _connectionPruner = connectionPruner;
        _circuitBreakerStore = circuitBreakerStore;
    }

    public UpstreamHealthStore(
        IProxyUpstreamHealthMetricsSink metrics,
        IUpstreamConnectionPruner connectionPruner)
        : this(metrics, connectionPruner, null)
    {
    }

    public bool IsUsable(RuntimeUpstream upstream)
    {
        return !_states.TryGetValue(upstream.Identity, out var state)
            || state.State != UpstreamHealthState.Unhealthy;
    }

    public void RecordSelection(RuntimeUpstream upstream)
    {
        var state = GetOrCreate(upstream);
        Interlocked.Increment(ref state.SelectedRequests);
    }

    public void RecordRequestFailure(RuntimeUpstream upstream)
    {
        var state = GetOrCreate(upstream);
        Interlocked.Increment(ref state.RequestFailures);
        _metrics.UpstreamRequestFailed(upstream);
    }

    public UpstreamHealthState RecordHealthCheckResult(
        RuntimeRoute route,
        RuntimeUpstream upstream,
        HealthCheckSample sample,
        DateTimeOffset checkedAtUtc)
    {
        var state = GetOrCreate(upstream);
        lock (state.Gate)
        {
            state.HealthCheckEnabled = route.HealthCheck.Enabled;
            state.LastResult = sample.Result;
            state.LastCheckedAtUtc = checkedAtUtc;
            var previous = state.State;

            if (sample.Healthy)
            {
                state.ConsecutiveSuccesses++;
                state.ConsecutiveFailures = 0;
                if (state.State != UpstreamHealthState.Healthy
                    && state.ConsecutiveSuccesses >= route.HealthCheck.HealthyThreshold)
                {
                    state.State = UpstreamHealthState.Healthy;
                }
            }
            else
            {
                state.ConsecutiveFailures++;
                state.ConsecutiveSuccesses = 0;
                if (state.State != UpstreamHealthState.Unhealthy
                    && state.ConsecutiveFailures >= route.HealthCheck.UnhealthyThreshold)
                {
                    state.State = UpstreamHealthState.Unhealthy;
                }
            }

            if (state.State != previous)
            {
                _metrics.UpstreamHealthTransition();
                if (state.State == UpstreamHealthState.Unhealthy)
                {
                    _connectionPruner.PruneIdleConnections(upstream);
                }
            }

            return state.State;
        }
    }

    public IReadOnlyList<ProxyUpstreamStatusResponse> Snapshot(ProxyConfigurationSnapshot? configuration)
    {
        if (configuration is null)
        {
            return [];
        }

        List<ProxyUpstreamStatusResponse> records = [];
        foreach (var route in configuration.Routes)
        {
            foreach (var upstream in route.Upstreams)
            {
                var state = GetOrCreate(upstream);
                lock (state.Gate)
                {
                    state.HealthCheckEnabled = route.HealthCheck.Enabled;
                    records.Add(new ProxyUpstreamStatusResponse(
                        upstream.RouteName,
                        upstream.Name,
                        upstream.Endpoint,
                        upstream.Scheme,
                        string.Equals(upstream.Scheme, "https", StringComparison.OrdinalIgnoreCase)
                            && upstream.Tls.ValidateCertificate,
                        string.Equals(upstream.Scheme, "https", StringComparison.OrdinalIgnoreCase)
                            ? upstream.EffectiveSniHost
                            : null,
                        route.HealthCheck.Enabled,
                        state.State,
                        state.LastResult,
                        state.LastCheckedAtUtc,
                        state.ConsecutiveSuccesses,
                        state.ConsecutiveFailures,
                        Interlocked.Read(ref state.SelectedRequests),
                        Interlocked.Read(ref state.RequestFailures))
                    {
                        Protocol = upstream.Protocol,
                        Weight = upstream.Weight,
                        CircuitBreaker = _circuitBreakerStore?.Snapshot(upstream) ?? DisabledCircuitBreaker(upstream)
                    });
                }
            }
        }

        return records;
    }

    public IReadOnlyList<ProxyUpstreamStatusResponse> ReadUpstreams(ProxyConfigurationSnapshot? configuration)
    {
        return Snapshot(configuration);
    }

    private static CircuitBreakerStatus DisabledCircuitBreaker(RuntimeUpstream upstream)
    {
        return new CircuitBreakerStatus(
            upstream.CircuitBreaker.Enabled ? CircuitBreakerRuntimeState.Closed : CircuitBreakerRuntimeState.Disabled,
            upstream.CircuitBreaker.Enabled,
            upstream.CircuitBreaker.FailureThreshold,
            upstream.CircuitBreaker.HalfOpenMaxAttempts,
            null,
            null,
            0,
            0,
            null);
    }

    private MutableUpstreamHealth GetOrCreate(RuntimeUpstream upstream)
    {
        return _states.GetOrAdd(
            upstream.Identity,
            _ => new MutableUpstreamHealth(upstream.RouteName, upstream.Name, upstream.Endpoint));
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
