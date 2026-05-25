using MDRAVA.API.Proxy.Configuration.Storage;

namespace MDRAVA.API.Proxy.Configuration.Runtime;

public sealed class ProxyConfigurationReadProjectionSource
    : IProxyConfigurationReadProjectionSource<ProxyConfigurationProjection>
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyConfigurationReadProjectionSource(IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public ProxyConfigurationProjection? ReadCurrent()
    {
        return _configurationStore.TryGetSnapshot(out var snapshot) && snapshot is not null
            ? ProxyConfigurationMapper.ToProjection(snapshot)
            : null;
    }
}
