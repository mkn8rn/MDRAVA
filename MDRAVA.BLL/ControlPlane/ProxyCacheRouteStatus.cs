namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyCacheRouteStatus(
    string RouteName,
    bool Enabled,
    long MaxEntryBytes,
    long MaxTotalBytes,
    int CurrentEntryCount,
    long CurrentBytes);
