namespace MDRAVA.API.Models.Diagnostics;

public sealed record RouteMatchDryRunPolicy(
    bool Enabled,
    bool WouldApply,
    string Reason);
