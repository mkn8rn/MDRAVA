using BusinessProxyCacheStatus = MDRAVA.BLL.ControlPlane.Caching.ProxyCacheStatus;

namespace MDRAVA.API.Controllers;

public sealed record ProxyCacheStatusResponse(
    int EntryCount,
    long ApproximateBytes,
    long HitCount,
    long MissCount,
    long StoreCount,
    long EvictionCount,
    long StoreRejectionCount,
    DateTimeOffset? LastClearedAtUtc,
    string? LastClearReason,
    IReadOnlyList<ProxyCacheRejectionStatusResponse> Rejections,
    IReadOnlyList<ProxyCacheRouteStatusResponse> Routes)
{
    public static ProxyCacheStatusResponse FromStatus(BusinessProxyCacheStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new ProxyCacheStatusResponse(
            EntryCount: status.EntryCount,
            ApproximateBytes: status.ApproximateBytes,
            HitCount: status.HitCount,
            MissCount: status.MissCount,
            StoreCount: status.StoreCount,
            EvictionCount: status.EvictionCount,
            StoreRejectionCount: status.StoreRejectionCount,
            LastClearedAtUtc: status.LastClearedAtUtc,
            LastClearReason: status.LastClearReason,
            Rejections: ProxyCacheRejectionStatusResponse.FromStatuses(status.Rejections),
            Routes: ProxyCacheRouteStatusResponse.FromStatuses(status.Routes));
    }
}
