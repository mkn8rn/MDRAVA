namespace MDRAVA.BLL.ControlPlane;

public sealed record RouteMatchDryRunRoute(
    string SiteName,
    string Name,
    string Host,
    string PathPrefix);
