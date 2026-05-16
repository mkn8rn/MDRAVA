using System.Collections.Concurrent;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Health;
using MDRAVA.API.Proxy.Metrics;

namespace MDRAVA.API.Proxy.Routing;

public sealed class RoundRobinUpstreamSelector : IUpstreamSelector
{
    private readonly UpstreamHealthStore _healthStore;
    private readonly ProxyMetrics _metrics;
    private readonly ConcurrentDictionary<string, int> _nextIndexes = new(StringComparer.OrdinalIgnoreCase);

    public RoundRobinUpstreamSelector(
        UpstreamHealthStore healthStore,
        ProxyMetrics metrics)
    {
        _healthStore = healthStore;
        _metrics = metrics;
    }

    public UpstreamSelection? Select(RuntimeRoute route)
    {
        if (route.Upstreams.Count == 0)
        {
            _metrics.NoHealthyUpstream();
            return null;
        }

        List<RuntimeUpstream> candidates = [];
        foreach (var upstream in route.Upstreams)
        {
            if (!route.HealthCheck.Enabled || _healthStore.IsUsable(upstream))
            {
                candidates.Add(upstream);
            }
        }

        if (candidates.Count == 0)
        {
            _metrics.NoHealthyUpstream();
            return null;
        }

        var index = _nextIndexes.AddOrUpdate(
            route.Name,
            1,
            (_, current) => current == int.MaxValue ? 0 : current + 1);
        var selected = candidates[Math.Abs(index - 1) % candidates.Count];
        _metrics.UpstreamSelected(selected);
        _healthStore.RecordSelection(selected);
        return new UpstreamSelection(route, selected);
    }
}
