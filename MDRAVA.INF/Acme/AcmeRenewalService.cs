
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MDRAVA.INF.Acme;

public sealed class AcmeRenewalService : BackgroundService
{
    private readonly IProxyConfigurationStore _configurationStore;
    private readonly AcmeCertificateManager _manager;
    private readonly AcmeRenewalSchedulePolicy _schedulePolicy;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AcmeRenewalService> _logger;

    public AcmeRenewalService(
        IProxyConfigurationStore configurationStore,
        AcmeCertificateManager manager,
        AcmeRenewalSchedulePolicy schedulePolicy,
        TimeProvider timeProvider,
        ILogger<AcmeRenewalService> logger)
    {
        _configurationStore = configurationStore;
        _manager = manager;
        _schedulePolicy = schedulePolicy;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _manager.CheckRenewalsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "ACME renewal check failed unexpectedly.");
            }

            var delay = ResolveDelay();
            try
            {
                await Task.Delay(delay, _timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private TimeSpan ResolveDelay()
    {
        _configurationStore.TryGetSnapshot(out var snapshot);
        return _schedulePolicy.ResolveDelay(snapshot);
    }
}
