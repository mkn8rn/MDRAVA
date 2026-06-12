using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Caching;

public interface IProxyCacheStatusConfigurationSource
{
    IReadOnlyList<ProxyCacheStatusRouteSource> ReadRoutes();
}

public interface IProxyCacheRuntimeStatusSource
{
    ProxyCacheRuntimeStatusSnapshot ReadSnapshot();
}

public sealed record ProxyCacheStatusRouteSource(
    string RouteName,
    bool Enabled,
    long MaxEntryBytes,
    long MaxTotalBytes);

public sealed record ProxyCacheRuntimeStatusSnapshot(
    int EntryCount,
    long ApproximateBytes,
    long HitCount,
    long MissCount,
    long StoreCount,
    long EvictionCount,
    long StoreRejectionCount,
    DateTimeOffset? LastClearedAtUtc,
    string? LastClearReason,
    IReadOnlyList<ProxyCacheRuntimeRejectionSnapshot> Rejections,
    IReadOnlyList<ProxyCacheRuntimeEntrySnapshot> Entries);

public sealed record ProxyCacheRuntimeRejectionSnapshot(
    string Reason,
    long Count);

public sealed record ProxyCacheRuntimeEntrySnapshot(
    string RouteName,
    long SizeBytes);

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
