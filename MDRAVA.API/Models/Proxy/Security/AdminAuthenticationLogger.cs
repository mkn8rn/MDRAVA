namespace MDRAVA.API.Proxy.Security;

public sealed class AdminAuthenticationLogger : IProxyAdminAuthenticationEventSink
{
    private readonly ILogger<AdminAuthenticationLogger> _logger;

    public AdminAuthenticationLogger(ILogger<AdminAuthenticationLogger> logger)
    {
        _logger = logger;
    }

    public void ActiveConfigurationMissing()
    {
        _logger.LogWarning("Admin request arrived before an active proxy configuration snapshot was available.");
    }
}
