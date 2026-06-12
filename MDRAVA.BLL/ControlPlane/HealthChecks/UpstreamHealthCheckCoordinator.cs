namespace MDRAVA.BLL.ControlPlane.HealthChecks;

public sealed class UpstreamHealthCheckCoordinator
{
    private readonly IUpstreamHealthCheckClient _client;
    private readonly UpstreamHealthStore _healthStore;
    private readonly IProxyHealthCheckMetricsSink _metrics;
    private readonly TimeProvider _timeProvider;
    private readonly IUpstreamHealthCheckEventSink _events;
    private readonly Dictionary<string, DateTimeOffset> _nextChecks = new(StringComparer.OrdinalIgnoreCase);

    public UpstreamHealthCheckCoordinator(
        IUpstreamHealthCheckClient client,
        UpstreamHealthStore healthStore,
        IProxyHealthCheckMetricsSink metrics,
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
        IReadOnlyList<UpstreamHealthCheckTarget> targets,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var target in targets)
        {
            if (_nextChecks.TryGetValue(target.Upstream.Identity, out var nextCheck)
                && nextCheck > now)
            {
                continue;
            }

            _nextChecks[target.Upstream.Identity] = now + target.Interval;
            await CheckUpstreamAsync(target, cancellationToken);
        }
    }

    private async ValueTask CheckUpstreamAsync(
        UpstreamHealthCheckTarget target,
        CancellationToken cancellationToken)
    {
        _metrics.HealthCheckAttempted();
        var sample = await _client.CheckAsync(target, cancellationToken);
        if (sample.Healthy)
        {
            _metrics.HealthCheckSucceeded();
        }
        else
        {
            _metrics.HealthCheckFailed();
        }

        var state = _healthStore.RecordHealthCheckResult(
            target,
            sample,
            _timeProvider.GetUtcNow());
        _events.Checked(target.RouteName, target.Upstream.Name, target.Upstream.Endpoint, sample.Result, state);
    }
}
