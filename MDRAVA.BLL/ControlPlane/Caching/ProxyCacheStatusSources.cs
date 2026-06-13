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

public sealed record ProxyCacheRuntimeStatusSnapshot
{
    public ProxyCacheRuntimeStatusSnapshot(
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
        IReadOnlyList<ProxyCacheRuntimeEntrySnapshot> Entries)
    {
        ArgumentNullException.ThrowIfNull(Rejections);
        ArgumentNullException.ThrowIfNull(Entries);

        this.EntryCount = EntryCount;
        this.ApproximateBytes = ApproximateBytes;
        this.HitCount = HitCount;
        this.MissCount = MissCount;
        this.StoreCount = StoreCount;
        this.EvictionCount = EvictionCount;
        this.StoreRejectionCount = StoreRejectionCount;
        this.LastClearedAtUtc = LastClearedAtUtc;
        this.LastClearReason = LastClearReason;
        this.Rejections = CacheList.Copy(Rejections);
        this.Entries = CacheList.Copy(Entries);
    }

    public int EntryCount { get; }

    public long ApproximateBytes { get; }

    public long HitCount { get; }

    public long MissCount { get; }

    public long StoreCount { get; }

    public long EvictionCount { get; }

    public long StoreRejectionCount { get; }

    public DateTimeOffset? LastClearedAtUtc { get; }

    public string? LastClearReason { get; }

    public IReadOnlyList<ProxyCacheRuntimeRejectionSnapshot> Rejections { get; }

    public IReadOnlyList<ProxyCacheRuntimeEntrySnapshot> Entries { get; }
}

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
