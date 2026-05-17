namespace MDRAVA.API.Models.Metrics;

public sealed record ProxyRouteDryRunFailureSnapshot(
    string Reason,
    long Count);
