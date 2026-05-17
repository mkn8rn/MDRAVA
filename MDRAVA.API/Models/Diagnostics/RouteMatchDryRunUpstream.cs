namespace MDRAVA.API.Models.Diagnostics;

public sealed record RouteMatchDryRunUpstream(
    string Name,
    string Scheme,
    string Protocol,
    string Endpoint,
    int Weight,
    string SelectionExplanation);
