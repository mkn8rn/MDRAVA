using MDRAVA.API.Proxy.Runtime;

namespace MDRAVA.API.Proxy.Configuration.Loading;

public sealed class ProxyConfigurationStartupService : IHostedService
{
    private readonly IProxyConfigurationReloadService _reloadService;
    private readonly ProxyRuntimePreflightService _preflight;
    private readonly ILogger<ProxyConfigurationStartupService> _logger;

    public ProxyConfigurationStartupService(
        IProxyConfigurationReloadService reloadService,
        ProxyRuntimePreflightService preflight,
        ILogger<ProxyConfigurationStartupService> logger)
    {
        _reloadService = reloadService;
        _preflight = preflight;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var preflight = _preflight.RunStartupChecks();
        if (string.Equals(preflight.State, "failed", StringComparison.OrdinalIgnoreCase))
        {
            var message = "MDRAVA runtime preflight failed: "
                + string.Join(", ", preflight.Reasons);
            _logger.LogCritical("{Message}", message);
            throw new InvalidOperationException(message);
        }

        if (string.Equals(preflight.State, "degraded", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "MDRAVA runtime preflight completed with warnings: {Reasons}",
                string.Join(", ", preflight.Reasons));
        }

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
