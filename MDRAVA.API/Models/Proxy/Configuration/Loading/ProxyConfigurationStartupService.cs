namespace MDRAVA.API.Proxy.Configuration.Loading;

public sealed class ProxyConfigurationStartupService : IHostedService
{
    private readonly IProxyConfigurationReloadService _reloadService;
    private readonly ILogger<ProxyConfigurationStartupService> _logger;

    public ProxyConfigurationStartupService(
        IProxyConfigurationReloadService reloadService,
        ILogger<ProxyConfigurationStartupService> logger)
    {
        _reloadService = reloadService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var result = await _reloadService.ReloadAsync(cancellationToken);
        if (!result.Succeeded)
        {
            var message = $"MDRAVA could not load a valid proxy configuration from '{result.SourceDirectory}'. "
                + string.Join(" ", result.Errors);
            _logger.LogCritical("{Message}", message);
            throw new InvalidOperationException(message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
