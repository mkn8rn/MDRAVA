namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyTrafficMetricsSnapshot(
    long Requests,
    long BytesRead,
    long BytesWritten);
