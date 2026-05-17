namespace MDRAVA.API.Models.Metrics;

public sealed record ProxyConfigLintFindingMetricSnapshot(
    string Severity,
    string Code,
    long Count);
