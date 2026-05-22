namespace MDRAVA.BLL.ControlPlane;

public sealed record RouteMatchDryRunFinding(
    string Severity,
    string Code,
    string Message);
