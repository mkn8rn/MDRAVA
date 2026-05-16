namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeRouteResolvedOptions(
    long MaxRequestBodyBytes,
    TimeSpan ClientRequestHeadTimeout,
    TimeSpan UpstreamResponseHeadTimeout,
    bool AccessLogEnabled);
