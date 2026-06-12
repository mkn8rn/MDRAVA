using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using System.Collections.Concurrent;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.UpstreamSelection;

public sealed class RoundRobinUpstreamSelector : IUpstreamSelector
{
    private readonly UpstreamHealthStore _healthStore;
    private readonly CircuitBreakerStore _circuitBreakerStore;
    private readonly IProxyUpstreamSelectionMetricsSink _metrics;
    private readonly ConcurrentDictionary<string, int> _nextIndexes = new(StringComparer.OrdinalIgnoreCase);

    public RoundRobinUpstreamSelector(
        UpstreamHealthStore healthStore,
        CircuitBreakerStore circuitBreakerStore,
        IProxyUpstreamSelectionMetricsSink metrics)
    {
        _healthStore = healthStore;
        _circuitBreakerStore = circuitBreakerStore;
        _metrics = metrics;
    }

    public UpstreamSelection? Select(UpstreamSelectionRoute route)
    {
        if (route.Upstreams.Count == 0)
        {
            _metrics.NoHealthyUpstream();
            _metrics.NoAvailableUpstream();
            return null;
        }

        List<RuntimeUpstream> candidates = [];
        foreach (var upstream in route.Upstreams)
        {
            if (route.HealthCheckEnabled && !_healthStore.IsUsable(upstream))
            {
                continue;
            }

            if (_circuitBreakerStore.IsAvailable(upstream))
            {
                candidates.Add(upstream);
                continue;
            }

            _circuitBreakerStore.RecordRejectedIfUnavailable(upstream);
        }

        if (candidates.Count == 0)
        {
            _metrics.NoHealthyUpstream();
            _metrics.NoAvailableUpstream();
            return null;
        }

        while (candidates.Count > 0)
        {
            var selected = SelectWeighted(route, candidates);
            if (_circuitBreakerStore.TryAcquire(selected, out var lease))
            {
                _metrics.UpstreamSelected(new ProxyUpstreamSelectionMetric(
                    selected.RouteName,
                    selected.Name,
                    selected.Scheme,
                    selected.Protocol));
                _healthStore.RecordSelection(selected);
                return new UpstreamSelection(selected, lease);
            }

            candidates.Remove(selected);
        }

        _metrics.NoAvailableUpstream();
        return null;
    }

    private RuntimeUpstream SelectWeighted(UpstreamSelectionRoute route, IReadOnlyList<RuntimeUpstream> candidates)
    {
        var totalWeight = candidates.Sum(static upstream => upstream.Weight);
        if (totalWeight <= 0)
        {
            return candidates[0];
        }

        var index = _nextIndexes.AddOrUpdate(
            route.Name,
            1,
            (_, current) => current == int.MaxValue ? 0 : current + 1);
        var position = Math.Abs(index - 1) % totalWeight;
        var cumulative = 0;
        foreach (var upstream in candidates)
        {
            cumulative += upstream.Weight;
            if (position < cumulative)
            {
                return upstream;
            }
        }

        return candidates[^1];
    }
}
