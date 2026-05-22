using MDRAVA.API.Proxy.Configuration.Storage;

namespace MDRAVA.API.Proxy.Caching;

public sealed class ProxyCacheStatusReader : IProxyCacheStatusReader
{
    private readonly ResponseCacheStore _cacheStore;
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyCacheStatusReader(
        ResponseCacheStore cacheStore,
        IProxyConfigurationStore configurationStore)
    {
        _cacheStore = cacheStore;
        _configurationStore = configurationStore;
    }

    public ProxyCacheStatusResponse GetStatus()
    {
        _configurationStore.TryGetSnapshot(out var snapshot);
        return _cacheStore.Snapshot(snapshot);
    }
}
