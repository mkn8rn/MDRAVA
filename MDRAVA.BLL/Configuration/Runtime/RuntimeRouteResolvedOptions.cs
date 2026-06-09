namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeRouteResolvedOptions(
    long MaxRequestBodyBytes,
    TimeSpan ClientRequestHeadTimeout,
    TimeSpan UpstreamResponseHeadTimeout,
    bool AccessLogEnabled);
