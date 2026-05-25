using MDRAVA.API.Proxy.Configuration.Storage;

namespace MDRAVA.API.Proxy.Configuration.Runtime;

public sealed class ProxyConfigurationReadOperations
    : IProxyConfigurationReadOperations<ProxyConfigurationProjection>
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyConfigurationReadOperations(IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public ProxyConfigurationReadResult<ProxyConfigurationProjection> ReadActive()
    {
        return ReadCurrentProjection();
    }

    public ProxyConfigurationReadResult<ProxyConfigurationProjection> ReadEffective()
    {
        return ReadCurrentProjection();
    }

    private ProxyConfigurationReadResult<ProxyConfigurationProjection> ReadCurrentProjection()
    {
        if (!_configurationStore.TryGetSnapshot(out var snapshot) || snapshot is null)
        {
            return new ProxyConfigurationReadResult<ProxyConfigurationProjection>(false, null);
        }

        return new ProxyConfigurationReadResult<ProxyConfigurationProjection>(
            true,
            ProxyConfigurationMapper.ToProjection(snapshot));
    }
}
