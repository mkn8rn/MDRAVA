namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed record RouteMatchDryRunUpstream(
    string Name,
    string Scheme,
    string Protocol,
    string Endpoint,
    int Weight,
    string SelectionReason);
