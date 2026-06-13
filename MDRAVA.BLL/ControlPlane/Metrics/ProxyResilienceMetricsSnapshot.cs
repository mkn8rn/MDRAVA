namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyResilienceMetricsSnapshot(
    long RetryAttempts,
    long RetryExhausted,
    IReadOnlyList<ProxyRetrySkippedSnapshot> RetrySkipped,
    long CircuitOpened,
    long CircuitHalfOpened,
    long CircuitClosed,
    long CircuitRejections,
    long NoAvailableUpstreamFailures)
{
    public IReadOnlyList<ProxyRetrySkippedSnapshot> RetrySkipped { get; } =
        MetricsList.Copy(RetrySkipped);
}
