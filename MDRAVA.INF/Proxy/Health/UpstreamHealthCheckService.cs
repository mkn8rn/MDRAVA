using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using Microsoft.Extensions.Hosting;

namespace MDRAVA.INF.Proxy.Health;

public sealed class UpstreamHealthCheckService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    private readonly IProxyConfigurationStore _configurationStore;
    private readonly UpstreamHealthCheckCoordinator _coordinator;
    private readonly TimeProvider _timeProvider;

    public UpstreamHealthCheckService(
        IProxyConfigurationStore configurationStore,
        UpstreamHealthCheckCoordinator coordinator,
        TimeProvider timeProvider)
    {
        _configurationStore = configurationStore;
        _coordinator = coordinator;
        _timeProvider = timeProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_configurationStore.TryGetSnapshot(out var snapshot) && snapshot is not null)
            {
                await _coordinator.RunDueChecksAsync(snapshot, stoppingToken);
            }

            await Task.Delay(PollInterval, _timeProvider, stoppingToken);
        }
    }
}
