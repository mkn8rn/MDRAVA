namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyClientConnectionMetricsSnapshot(
    long Accepted,
    long Active,
    long ClosedByIdleTimeout,
    long ClosedByMaxRequests);
