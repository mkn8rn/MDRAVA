namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyAdminAuthMetricsSnapshot(
    long Successes,
    long Failures);
