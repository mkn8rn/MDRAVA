namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyRejectionMetricsSnapshot(
    long ClientConnectionAdmissionRejections,
    long RateLimitedRequests,
    long RateLimitedUpgrades,
    long RequestBodySizeRejections,
    long ParserLimitRejections,
    long MalformedRequests,
    long UnsupportedRequestFraming);
