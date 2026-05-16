namespace MDRAVA.API.Proxy.Metrics;

public sealed record ProxyMetricsSnapshot(
    long AcceptedConnections,
    long ActiveConnections,
    long TotalRequests,
    long UpstreamSuccesses,
    long UpstreamFailures,
    long BytesRead,
    long BytesWritten,
    long ParseErrors,
    long RejectedMalformedRequests,
    long RejectedUnsupportedRequestFraming,
    long UpstreamMalformedResponses,
    long ClientBodyRelayFailures,
    long UpstreamBodyRelayFailures);
