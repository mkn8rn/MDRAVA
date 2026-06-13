namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyUpstreamForwardingMetricsSnapshot(
    long Successes,
    long Failures,
    long BodyRelayFailures);
