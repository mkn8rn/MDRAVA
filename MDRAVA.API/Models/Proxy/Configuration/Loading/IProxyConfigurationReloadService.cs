namespace MDRAVA.API.Proxy.Configuration.Loading;

public interface IProxyConfigurationReloadService
{
    ValueTask<ProxyConfigurationReloadResult> ReloadAsync(CancellationToken cancellationToken);

    ValueTask<ProxyConfigurationValidationResult> ValidateAsync(CancellationToken cancellationToken);
}
