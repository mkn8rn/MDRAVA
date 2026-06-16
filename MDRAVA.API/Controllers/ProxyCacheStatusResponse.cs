using BusinessProxyCacheStatus = MDRAVA.BLL.ControlPlane.Caching.ProxyCacheStatus;

namespace MDRAVA.API.Controllers;

public sealed record ProxyCacheStatusResponse
{
    public ProxyCacheStatusResponse(
        int entryCount,
        long approximateBytes,
        long hitCount,
        long missCount,
        long storeCount,
        long evictionCount,
        long storeRejectionCount,
        DateTimeOffset? lastClearedAtUtc,
        string? lastClearReason,
        IReadOnlyList<ProxyCacheRejectionStatusResponse> rejections,
        IReadOnlyList<ProxyCacheRouteStatusResponse> routes)
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
        Rejections = ApiResponseList.Copy(rejections);
        Routes = ApiResponseList.Copy(routes);
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

    public IReadOnlyList<ProxyCacheRejectionStatusResponse> Rejections { get; }

    public IReadOnlyList<ProxyCacheRouteStatusResponse> Routes { get; }

    public static ProxyCacheStatusResponse FromStatus(BusinessProxyCacheStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new ProxyCacheStatusResponse(
            entryCount: status.EntryCount,
            approximateBytes: status.ApproximateBytes,
            hitCount: status.HitCount,
            missCount: status.MissCount,
            storeCount: status.StoreCount,
            evictionCount: status.EvictionCount,
            storeRejectionCount: status.StoreRejectionCount,
            lastClearedAtUtc: status.LastClearedAtUtc,
            lastClearReason: status.LastClearReason,
            rejections: ProxyCacheRejectionStatusResponse.FromStatuses(status.Rejections),
            routes: ProxyCacheRouteStatusResponse.FromStatuses(status.Routes));
    }
}
