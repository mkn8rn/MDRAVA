using BusinessProxyCacheRejectionStatus = MDRAVA.BLL.ControlPlane.Caching.ProxyCacheRejectionStatus;
using BusinessProxyCacheRouteStatus = MDRAVA.BLL.ControlPlane.Caching.ProxyCacheRouteStatus;
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

public sealed record ProxyCacheRejectionStatusResponse(
    string Reason,
    long Count)
{
    public static IReadOnlyList<ProxyCacheRejectionStatusResponse> FromStatuses(
        IReadOnlyList<BusinessProxyCacheRejectionStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        return statuses.Select(FromStatus).ToArray();
    }

    private static ProxyCacheRejectionStatusResponse FromStatus(BusinessProxyCacheRejectionStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new ProxyCacheRejectionStatusResponse(status.Reason, status.Count);
    }
}

public sealed record ProxyCacheRouteStatusResponse(
    string RouteName,
    bool Enabled,
    long MaxEntryBytes,
    long MaxTotalBytes,
    int CurrentEntryCount,
    long CurrentBytes)
{
    public static IReadOnlyList<ProxyCacheRouteStatusResponse> FromStatuses(
        IReadOnlyList<BusinessProxyCacheRouteStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        return statuses.Select(FromStatus).ToArray();
    }

    private static ProxyCacheRouteStatusResponse FromStatus(BusinessProxyCacheRouteStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new ProxyCacheRouteStatusResponse(
            status.RouteName,
            status.Enabled,
            status.MaxEntryBytes,
            status.MaxTotalBytes,
            status.CurrentEntryCount,
            status.CurrentBytes);
    }
}
