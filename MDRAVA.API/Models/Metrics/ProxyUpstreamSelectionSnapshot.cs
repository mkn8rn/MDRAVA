namespace MDRAVA.API.Models.Metrics;

public sealed record ProxyUpstreamSelectionSnapshot(
    string Route,
    string Upstream,
    string Scheme,
    long Count);
