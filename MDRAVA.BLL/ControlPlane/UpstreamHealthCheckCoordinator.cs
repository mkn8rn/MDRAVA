using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.BLL.ControlPlane;

public sealed class UpstreamHealthCheckCoordinator
{
    private readonly IUpstreamHealthCheckClient _client;
    private readonly UpstreamHealthStore _healthStore;
    private readonly ProxyMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly IUpstreamHealthCheckEventSink _events;
    private readonly Dictionary<string, DateTimeOffset> _nextChecks = new(StringComparer.OrdinalIgnoreCase);

    public UpstreamHealthCheckCoordinator(
        IUpstreamHealthCheckClient client,
        UpstreamHealthStore healthStore,
        ProxyMetrics metrics,
        TimeProvider timeProvider,
        IUpstreamHealthCheckEventSink events)
    {
        _client = client;
        _healthStore = healthStore;
        _metrics = metrics;
        _timeProvider = timeProvider;
        _events = events;
    }

    public async ValueTask RunDueChecksAsync(
        ProxyConfigurationSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var route in snapshot.Routes)
        {
            if (!route.HealthCheck.Enabled)
            {
                continue;
            }

            foreach (var upstream in route.Upstreams)
            {
                if (_nextChecks.TryGetValue(upstream.Identity, out var nextCheck)
                    && nextCheck > now)
                {
                    continue;
                }

                _nextChecks[upstream.Identity] = now + route.HealthCheck.Interval;
                await CheckUpstreamAsync(route, upstream, cancellationToken);
            }
        }
    }

    private async ValueTask CheckUpstreamAsync(
        RuntimeRoute route,
        RuntimeUpstream upstream,
        CancellationToken cancellationToken)
    {
        _metrics.HealthCheckAttempted();
        var sample = await _client.CheckAsync(route, upstream, cancellationToken);
        if (sample.Healthy)
        {
            _metrics.HealthCheckSucceeded();
        }
        else
        {
            _metrics.HealthCheckFailed();
        }

        var state = _healthStore.RecordHealthCheckResult(
            route,
            upstream,
            sample,
            _timeProvider.GetUtcNow());
        _events.Checked(route.Name, upstream.Name, upstream.Endpoint, sample.Result, state);
    }
}
