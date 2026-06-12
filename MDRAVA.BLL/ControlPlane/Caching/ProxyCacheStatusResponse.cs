namespace MDRAVA.BLL.ControlPlane.Caching;

public sealed record ProxyCacheStatusResponse
{
    private ProxyCacheStatusResponse(
        int entryCount,
        long approximateBytes,
        long hitCount,
        long missCount,
        long storeCount,
        long evictionCount,
        long storeRejectionCount,
        DateTimeOffset? lastClearedAtUtc,
        string? lastClearReason,
        IReadOnlyList<ProxyCacheRejectionStatus> rejections,
        IReadOnlyList<ProxyCacheRouteStatus> routes)
    {
        EntryCount = entryCount;
        ApproximateBytes = approximateBytes;
        HitCount = hitCount;
        MissCount = missCount;
        StoreCount = storeCount;
        EvictionCount = evictionCount;
        StoreRejectionCount = storeRejectionCount;
        LastClearedAtUtc = lastClearedAtUtc;
        LastClearReason = lastClearReason;
        Rejections = rejections;
        Routes = routes;
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

    public IReadOnlyList<ProxyCacheRejectionStatus> Rejections { get; }

    public IReadOnlyList<ProxyCacheRouteStatus> Routes { get; }

    public static ProxyCacheStatusResponse FromRuntimeSnapshot(
        ProxyCacheRuntimeStatusSnapshot runtime,
        IReadOnlyList<ProxyCacheRejectionStatus> rejections,
        IReadOnlyList<ProxyCacheRouteStatus> routes)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(rejections);
        ArgumentNullException.ThrowIfNull(routes);

        return new ProxyCacheStatusResponse(
            runtime.EntryCount,
            runtime.ApproximateBytes,
            runtime.HitCount,
            runtime.MissCount,
            runtime.StoreCount,
            runtime.EvictionCount,
            runtime.StoreRejectionCount,
            runtime.LastClearedAtUtc,
            runtime.LastClearReason,
            rejections,
            routes);
    }
}
