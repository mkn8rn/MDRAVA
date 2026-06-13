namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class ProxyMetrics
{
    public void UpstreamConnectionOpened() => Interlocked.Increment(ref _upstreamConnectionsOpened);

    public void UpstreamConnectionReused() => Interlocked.Increment(ref _upstreamConnectionsReused);

    public void UpstreamConnectionDiscarded() => Interlocked.Increment(ref _upstreamConnectionsDiscarded);
}
