namespace MDRAVA.API.Proxy.Configuration.Loading;

public interface IProxyConfigurationNormalizer
{
    ProxyConfigurationNormalizeResult Normalize(ProxyConfigurationNormalizeRequest request);
}
