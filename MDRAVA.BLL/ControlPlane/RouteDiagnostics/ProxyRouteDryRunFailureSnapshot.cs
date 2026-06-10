namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed record ProxyRouteDryRunFailureSnapshot(
    string Reason,
    long Count);
