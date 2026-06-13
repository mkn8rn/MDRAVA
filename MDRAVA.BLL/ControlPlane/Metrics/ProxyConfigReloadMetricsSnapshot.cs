namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyConfigReloadMetricsSnapshot(
    long Successes,
    long Failures);
