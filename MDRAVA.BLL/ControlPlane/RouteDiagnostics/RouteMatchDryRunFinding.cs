namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed record RouteMatchDryRunFinding(
    string Severity,
    string Code,
    string Message);
