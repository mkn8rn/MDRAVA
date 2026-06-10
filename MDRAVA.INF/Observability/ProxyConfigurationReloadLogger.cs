using MDRAVA.BLL.ControlPlane;
using Microsoft.Extensions.Logging;

namespace MDRAVA.INF.Observability;

public sealed class ProxyConfigurationReloadLogger : IProxyConfigurationReloadEventSink
{
    private readonly ILogger<ProxyConfigurationReloadLogger> _logger;

    public ProxyConfigurationReloadLogger(ILogger<ProxyConfigurationReloadLogger> logger)
    {
        _logger = logger;
    }

    public void LoadFailed(string sourceDirectory, IReadOnlyList<string> errors)
    {
        _logger.LogWarning(
            "Proxy configuration reload failed from {SourcePath}: {Errors}",
            sourceDirectory,
            string.Join("; ", errors));
    }

    public void Loaded(int version, string sourceDirectory)
    {
        _logger.LogInformation(
            "Proxy configuration version {Version} loaded from {SourcePath}",
            version,
            sourceDirectory);
    }
}
