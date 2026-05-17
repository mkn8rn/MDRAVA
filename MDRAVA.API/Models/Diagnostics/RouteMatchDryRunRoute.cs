namespace MDRAVA.API.Models.Diagnostics;

public sealed record RouteMatchDryRunRoute(
    string SiteName,
    string Name,
    string Host,
    string PathPrefix);
