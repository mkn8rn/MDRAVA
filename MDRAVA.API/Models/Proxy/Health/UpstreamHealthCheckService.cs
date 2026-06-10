
namespace MDRAVA.INF.Proxy.Health;

public sealed class UpstreamHealthCheckService : BackgroundService
{
    private readonly IProxyConfigurationStore _configurationStore;
    private readonly UpstreamHealthCheckCoordinator _coordinator;

    public UpstreamHealthCheckService(
        IProxyConfigurationStore configurationStore,
        UpstreamHealthCheckCoordinator coordinator)
    {
        _configurationStore = configurationStore;
        _coordinator = coordinator;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_configurationStore.TryGetSnapshot(out var snapshot) && snapshot is not null)
            {
                await _coordinator.RunDueChecksAsync(snapshot, stoppingToken);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), stoppingToken);
        }
    }
}
