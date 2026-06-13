namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyAcmeRenewalMetricsSnapshot(
    long Attempts,
    long Successes,
    long Failures);
