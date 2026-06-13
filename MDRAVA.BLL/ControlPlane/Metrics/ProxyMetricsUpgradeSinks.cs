namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class ProxyMetrics
{
    public void UpgradeRequestReceived() => Interlocked.Increment(ref _upgradeRequestsReceived);

    public void UpgradeRequestSucceeded() => Interlocked.Increment(ref _upgradeRequestsSucceeded);

    public void UpgradeRequestRejected() => Interlocked.Increment(ref _upgradeRequestsRejected);

    public void UpgradeUpstreamFailed() => Interlocked.Increment(ref _upgradeUpstreamFailures);
}
