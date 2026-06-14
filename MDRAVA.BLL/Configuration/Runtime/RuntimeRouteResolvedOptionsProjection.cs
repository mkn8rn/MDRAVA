namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeRouteResolvedOptionsProjection(
    long MaxRequestBodyBytes,
    TimeSpan ClientRequestHeadTimeout,
    TimeSpan UpstreamResponseHeadTimeout,
    bool AccessLogEnabled);
