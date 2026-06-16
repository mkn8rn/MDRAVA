using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Caching;

public static class ProxyCacheStatusRouteSourceMapper
{
    public static IReadOnlyList<ProxyCacheStatusRouteSource> ToRouteSources(IEnumerable<RuntimeRoute> routes)
    {
        return CacheList.Copy(routes
            .Select(static route => new ProxyCacheStatusRouteSource(
                route.Name,
                route.Cache.Enabled,
                route.Cache.MaxEntryBytes,
                route.Cache.MaxTotalBytes)));
    }
}
