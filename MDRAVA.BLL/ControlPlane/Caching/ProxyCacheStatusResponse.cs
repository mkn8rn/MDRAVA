namespace MDRAVA.BLL.ControlPlane.Caching;

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
    IReadOnlyList<ProxyCacheRouteStatus> Routes);
