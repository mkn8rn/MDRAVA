namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyTunnelMetricsSnapshot(
    long Active,
    long Total,
    long IdleTimeouts,
    long BytesClientToUpstream,
    long BytesUpstreamToClient,
    long RelayFailures);
