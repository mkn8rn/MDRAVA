namespace MDRAVA.BLL.ControlPlane.Caching;

public sealed record ProxyCacheRejectionStatus(
    string Reason,
    long Count);
