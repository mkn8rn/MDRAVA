namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed record RouteMatchDryRunListener(
    string Name,
    string Transport,
    string Address,
    int Port,
    string Protocols);
