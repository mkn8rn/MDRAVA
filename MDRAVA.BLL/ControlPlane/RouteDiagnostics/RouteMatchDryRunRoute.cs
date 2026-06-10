namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed record RouteMatchDryRunRoute(
    string SiteName,
    string Name,
    string Host,
    string PathPrefix);
