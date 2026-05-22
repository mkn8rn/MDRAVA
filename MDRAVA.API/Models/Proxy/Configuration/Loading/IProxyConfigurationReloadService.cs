namespace MDRAVA.API.Proxy.Configuration.Loading;

public interface IProxyConfigurationReloadService
    : IProxyConfigurationValidationOperations
{
    ValueTask<ProxyConfigurationReloadResult> ReloadAsync(CancellationToken cancellationToken);
}
