namespace MDRAVA.API.Proxy.Configuration.Loading;

public interface IProxyConfigurationLoader
{
    ValueTask<ProxyConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken);

    ValueTask<ProxyConfigurationLoadResult> ValidateAsync(CancellationToken cancellationToken);

    ValueTask<ProxyConfigurationLoadResult> ValidateExistingLayoutAsync(CancellationToken cancellationToken);
}
