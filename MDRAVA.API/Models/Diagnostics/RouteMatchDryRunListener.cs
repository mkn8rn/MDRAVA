namespace MDRAVA.API.Models.Diagnostics;

public sealed record RouteMatchDryRunListener(
    string Name,
    string Transport,
    string Address,
    int Port,
    string Protocols);
