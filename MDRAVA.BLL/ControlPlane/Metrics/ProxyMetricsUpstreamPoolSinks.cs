namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class ProxyMetrics
{
    public void UpstreamPoolConnectionBorrowed()
    {
        Interlocked.Increment(ref _upstreamPoolActiveConnections);
    }

    public void UpstreamPoolConnectionReturnedIdle()
    {
        Interlocked.Decrement(ref _upstreamPoolActiveConnections);
        Interlocked.Increment(ref _upstreamPoolIdleConnections);
    }

    public void UpstreamPoolConnectionReusedFromIdle()
    {
        Interlocked.Decrement(ref _upstreamPoolIdleConnections);
        Interlocked.Increment(ref _upstreamPoolActiveConnections);
    }

    public void UpstreamPoolConnectionClosedActive()
    {
        Interlocked.Decrement(ref _upstreamPoolActiveConnections);
    }

    public void UpstreamPoolIdleConnectionDiscarded()
    {
        Interlocked.Decrement(ref _upstreamPoolIdleConnections);
    }
}
