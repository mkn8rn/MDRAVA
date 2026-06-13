using MDRAVA.BLL.ControlPlane.Caching;

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
    IReadOnlyList<ProxyCacheRejectionStatus> Rejections,
    IReadOnlyList<ProxyCacheRouteStatus> Routes)
{
    public static ProxyCacheStatusResponse FromStatus(ProxyCacheStatus status)
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
            Rejections: status.Rejections,
            Routes: status.Routes);
    }
}
