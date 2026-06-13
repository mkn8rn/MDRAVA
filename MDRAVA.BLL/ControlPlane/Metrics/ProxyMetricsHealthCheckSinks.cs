namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class ProxyMetrics
{
    public void HealthCheckAttempted() => Interlocked.Increment(ref _healthChecksAttempted);

    public void HealthCheckSucceeded() => Interlocked.Increment(ref _healthChecksSucceeded);

    public void HealthCheckFailed() => Interlocked.Increment(ref _healthChecksFailed);

    public void UpstreamHealthTransition() => Interlocked.Increment(ref _upstreamHealthTransitions);
}
