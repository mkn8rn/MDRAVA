using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Caching;

namespace MDRAVA.INF.Proxy.Caching;

public static class ProxyCacheStatusRouteSourceMapper
{
    public static IReadOnlyList<ProxyCacheStatusRouteSource> ToRouteSources(IReadOnlyList<RuntimeRoute> routes)
    {
        return routes
            .Select(static route => new ProxyCacheStatusRouteSource(
                route.Name,
                route.Cache.Enabled,
                route.Cache.MaxEntryBytes,
                route.Cache.MaxTotalBytes))
            .ToArray();
    }
}
