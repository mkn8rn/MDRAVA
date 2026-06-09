
namespace MDRAVA.API.Proxy.Caching;

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
            ? ProxyCacheStatusRuntimeRouteSourceMapper.ToRouteSources(snapshot)
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

internal static class ProxyCacheStatusRuntimeRouteSourceMapper
{
    public static IReadOnlyList<ProxyCacheStatusRouteSource> ToRouteSources(ProxyConfigurationSnapshot? snapshot)
    {
        return snapshot?.Routes
            .Select(static route => new ProxyCacheStatusRouteSource(
                route.Name,
                route.Cache.Enabled,
                route.Cache.MaxEntryBytes,
                route.Cache.MaxTotalBytes))
            .ToArray() ?? [];
    }
}
