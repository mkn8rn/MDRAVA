namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyRouteDryRunFailureSnapshot(
    string Reason,
    long Count);
