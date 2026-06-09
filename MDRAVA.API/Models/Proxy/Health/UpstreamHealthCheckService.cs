using MDRAVA.API.Proxy.Metrics;

namespace MDRAVA.API.Proxy.Health;

public sealed class UpstreamHealthCheckService : BackgroundService
{
    private readonly IProxyConfigurationStore _configurationStore;
    private readonly UpstreamHealthCheckClient _client;
    private readonly UpstreamHealthStore _healthStore;
    private readonly ProxyMetrics _metrics;
    private readonly ILogger<UpstreamHealthCheckService> _logger;
    private readonly Dictionary<string, DateTimeOffset> _nextChecks = new(StringComparer.OrdinalIgnoreCase);

    public UpstreamHealthCheckService(
        IProxyConfigurationStore configurationStore,
        UpstreamHealthCheckClient client,
        UpstreamHealthStore healthStore,
        ProxyMetrics metrics,
        ILogger<UpstreamHealthCheckService> logger)
    {
        _configurationStore = configurationStore;
        _client = client;
        _healthStore = healthStore;
        _metrics = metrics;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_configurationStore.TryGetSnapshot(out var snapshot) && snapshot is not null)
            {
                await RunDueChecksAsync(snapshot, stoppingToken);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
        }
    }

    private async ValueTask RunDueChecksAsync(
        ProxyConfigurationSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
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

        var state = _healthStore.RecordHealthCheckResult(route, upstream, sample, DateTimeOffset.UtcNow);
        _logger.LogDebug(
            "Health check for route {RouteName} upstream {UpstreamName} at {Endpoint} returned {Result}; state is {HealthState}",
            route.Name,
            upstream.Name,
            upstream.Endpoint,
            sample.Result,
            state);
    }
}
