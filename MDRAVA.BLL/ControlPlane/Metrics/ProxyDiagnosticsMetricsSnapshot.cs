namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyDiagnosticsMetricsSnapshot(
    long RequestIdsGenerated,
    long AccessLogsEmitted,
    long RecentDiagnosticsOverwritten);
