using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.BLL.ControlPlane.Caching;

public sealed class ProxyCacheStatusConfigurationSource
    : IProxyCacheStatusConfigurationSource
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyCacheStatusConfigurationSource(IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public IReadOnlyList<ProxyCacheStatusRouteSource> ReadRoutes()
    {
        return _configurationStore.ReadSnapshot() is ProxyConfigurationSnapshotReadResult.AvailableResult available
            ? ProxyCacheStatusRouteSourceMapper.ToRouteSources(available.Snapshot.Routes)
            : [];
    }
}

public sealed class ProxyCacheRuntimeStatusSource
    : IProxyCacheRuntimeStatusSource
{
    private readonly ResponseCacheStore _cacheStore;

    public ProxyCacheRuntimeStatusSource(ResponseCacheStore cacheStore)
    {
        _cacheStore = cacheStore;
    }

    public ProxyCacheRuntimeStatusSnapshot ReadSnapshot()
    {
        return _cacheStore.ReadStatusSnapshot();
    }
}
