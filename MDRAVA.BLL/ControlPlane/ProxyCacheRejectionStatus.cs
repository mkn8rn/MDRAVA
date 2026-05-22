namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyCacheRejectionStatus(
    string Reason,
    long Count);
