using BusinessProxyCacheRejectionStatus = MDRAVA.BLL.ControlPlane.Caching.ProxyCacheRejectionStatus;
using BusinessProxyCacheRouteStatus = MDRAVA.BLL.ControlPlane.Caching.ProxyCacheRouteStatus;

namespace MDRAVA.API.Controllers;

public sealed record ProxyCacheRejectionStatusResponse(
    string Reason,
    long Count)
{
    public static IReadOnlyList<ProxyCacheRejectionStatusResponse> FromStatuses(
        IReadOnlyList<BusinessProxyCacheRejectionStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        return ApiResponseList.Copy(statuses.Select(FromStatus));
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

        return ApiResponseList.Copy(statuses.Select(FromStatus));
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
