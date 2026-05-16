using MDRAVA.API.Proxy.Configuration.Storage;

namespace MDRAVA.API.Proxy.Acme;

public sealed class AcmeRenewalService : BackgroundService
{
    private readonly IProxyConfigurationStore _configurationStore;
    private readonly AcmeCertificateManager _manager;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AcmeRenewalService> _logger;

    public AcmeRenewalService(
        IProxyConfigurationStore configurationStore,
        AcmeCertificateManager manager,
        TimeProvider timeProvider,
        ILogger<AcmeRenewalService> logger)
    {
        _configurationStore = configurationStore;
        _manager = manager;
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
        if (_configurationStore.TryGetSnapshot(out var snapshot) && snapshot is not null && snapshot.Acme.Enabled)
        {
            return TimeSpan.FromMinutes(Math.Clamp(snapshot.Acme.CheckIntervalMinutes, 5, 1440));
        }

        return TimeSpan.FromHours(12);
    }
}
