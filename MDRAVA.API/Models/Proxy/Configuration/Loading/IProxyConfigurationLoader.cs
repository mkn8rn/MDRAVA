namespace MDRAVA.API.Proxy.Configuration.Loading;

public interface IProxyConfigurationLoader
{
    ValueTask<ProxyConfigurationLoadResult> LoadAsync(CancellationToken cancellationToken);
}
