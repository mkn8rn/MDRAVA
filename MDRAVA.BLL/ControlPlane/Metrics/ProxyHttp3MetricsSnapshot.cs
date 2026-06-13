namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyHttp3MetricsSnapshot(
    long AcceptedConnections,
    long ActiveConnections,
    long Requests,
    long ProxiedRequests,
    long GeneratedResponses,
    long ActiveStreams,
    long StreamResets,
    long StreamedResponses,
    long ActiveResponseStreams,
    long ResponseBytesSent,
    long RequestBodyBytesReceived,
    long ResponseStreamResets,
    long AltSvcEmitted,
    long AltSvcSuppressed,
    IReadOnlyList<ProxyHttp3RequestOutcomeSnapshot> RequestsByOutcome,
    IReadOnlyDictionary<string, long> RejectedRequests,
    IReadOnlyDictionary<string, long> ProtocolErrors,
    long QuicListenerStartSuccesses,
    long QuicListenerStartFailures,
    long ActiveQuicListeners)
{
    public IReadOnlyList<ProxyHttp3RequestOutcomeSnapshot> RequestsByOutcome { get; } =
        MetricsList.Copy(RequestsByOutcome);

    public IReadOnlyDictionary<string, long> RejectedRequests { get; } =
        MetricsList.CopyDictionary(RejectedRequests, StringComparer.Ordinal);

    public IReadOnlyDictionary<string, long> ProtocolErrors { get; } =
        MetricsList.CopyDictionary(ProtocolErrors, StringComparer.Ordinal);
}
