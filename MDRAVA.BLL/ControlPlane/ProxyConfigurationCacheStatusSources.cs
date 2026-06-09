using MDRAVA.BLL.Infrastructure;

namespace MDRAVA.BLL.ControlPlane;

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
        return _configurationStore.TryGetSnapshot(out var snapshot) && snapshot is not null
            ? ProxyCacheStatusRouteSourceMapper.ToRouteSources(snapshot)
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
