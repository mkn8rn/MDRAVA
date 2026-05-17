namespace MDRAVA.API.Models.Diagnostics;

public sealed record RouteMatchDryRunFinding(
    string Severity,
    string Code,
    string Message);
