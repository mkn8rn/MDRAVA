namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class ProxyMetrics
{
    public void RequestIdGenerated() => Interlocked.Increment(ref _requestIdsGenerated);

    public void AccessLogEmitted() => Interlocked.Increment(ref _accessLogsEmitted);

    public void RecentDiagnosticOverwritten() => Interlocked.Increment(ref _recentDiagnosticsOverwritten);
}
