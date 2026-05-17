namespace MDRAVA.API.Models.Metrics;

public sealed record ProxyRetrySkippedSnapshot(
    string Reason,
    long Count);
