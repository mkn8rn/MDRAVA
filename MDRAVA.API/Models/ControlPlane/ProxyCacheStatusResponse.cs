namespace MDRAVA.API.Models.ControlPlane;

public sealed record ProxyCacheStatusResponse(
    int EntryCount,
    long ApproximateBytes,
    long HitCount,
    long MissCount,
    long EvictionCount,
    long StoreRejectionCount,
    DateTimeOffset? LastClearedAtUtc,
    string? LastClearReason,
    IReadOnlyList<ProxyCacheRejectionStatus> Rejections,
    IReadOnlyList<ProxyCacheRouteStatus> Routes);
