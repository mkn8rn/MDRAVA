namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed record RouteMatchDryRunPolicy(
    bool Enabled,
    bool WouldApply,
    string Reason);
