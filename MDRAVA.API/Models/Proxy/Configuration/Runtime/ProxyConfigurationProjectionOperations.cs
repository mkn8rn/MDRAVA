using MDRAVA.API.Proxy.Configuration.Storage;

namespace MDRAVA.API.Proxy.Configuration.Runtime;

public sealed class ProxyConfigurationProjectionOperations
    : IProxyConfigurationProjectionOperations<ProxyConfigurationProjection>
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyConfigurationProjectionOperations(IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public ProxyConfigurationProjectionReadResult<ProxyConfigurationProjection> GetActive()
    {
        return ReadCurrentProjection();
    }

    public ProxyConfigurationProjectionReadResult<ProxyConfigurationProjection> GetEffective()
    {
        return ReadCurrentProjection();
    }

    private ProxyConfigurationProjectionReadResult<ProxyConfigurationProjection> ReadCurrentProjection()
    {
        if (!_configurationStore.TryGetSnapshot(out var snapshot) || snapshot is null)
        {
            return new ProxyConfigurationProjectionReadResult<ProxyConfigurationProjection>(false, null);
        }

        return new ProxyConfigurationProjectionReadResult<ProxyConfigurationProjection>(
            true,
            ProxyConfigurationMapper.ToProjection(snapshot));
    }
}
