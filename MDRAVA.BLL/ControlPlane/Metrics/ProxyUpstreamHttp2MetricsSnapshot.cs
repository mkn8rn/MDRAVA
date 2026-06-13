namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyUpstreamHttp2MetricsSnapshot(
    long Requests,
    long AlpnFailures,
    long ProtocolErrors);
