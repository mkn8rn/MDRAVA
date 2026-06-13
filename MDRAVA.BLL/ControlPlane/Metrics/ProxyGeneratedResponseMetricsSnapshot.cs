namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyGeneratedResponseMetricsSnapshot(
    long BadGatewayResponses,
    long GatewayTimeoutResponses);
