using MDRAVA.BLL.ControlPlane.ConfigLint;
using MDRAVA.BLL.ControlPlane.RouteDiagnostics;

namespace MDRAVA.BLL.ControlPlane.Metrics;

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
    long UpstreamBodyRelayFailures,
    long ClientRequestHeadTimeouts,
    long ClientRequestBodyTimeouts,
    long UpstreamConnectFailures,
    long UpstreamConnectTimeouts,
    long UpstreamResponseHeadTimeouts,
    long UpstreamResponseBodyTimeouts,
    long UpstreamPrematureDisconnects,
    long ClientPrematureDisconnects,
    long ProxyGenerated502Responses,
    long ProxyGenerated504Responses,
    long DownstreamWriteTimeouts,
    long TlsHandshakeAttempts,
    long TlsHandshakeSuccesses,
    long TlsHandshakeFailures,
    long TlsHandshakeTimeouts,
    long TlsNoCertificateForSniFailures,
    long ClientConnectionsClosedByIdleTimeout,
    long ClientConnectionsClosedByMaxRequests,
    long UpstreamConnectionsOpened,
    long UpstreamConnectionsReused,
    long UpstreamConnectionsDiscarded,
    long UpstreamPoolIdleConnections,
    long UpstreamPoolActiveConnections,
    long UpgradeRequestsReceived,
    long UpgradeRequestsSucceeded,
    long UpgradeRequestsRejected,
    long UpgradeUpstreamFailures,
    long ActiveTunnels,
    long TotalTunnels,
    long TunnelIdleTimeouts,
    long TunnelBytesClientToUpstream,
    long TunnelBytesUpstreamToClient,
    long TunnelRelayFailures,
    long UpstreamSelections,
    long NoHealthyUpstreamFailures,
    long HealthChecksAttempted,
    long HealthChecksSucceeded,
    long HealthChecksFailed,
    long UpstreamHealthTransitions,
    long UpstreamRequestFailures,
    long RequestIdsGenerated,
    long AccessLogsEmitted,
    long RecentDiagnosticsOverwritten,
    long ConnectionAdmissionRejections,
    long ActiveTlsHandshakes,
    long TlsHandshakeAdmissionRejections,
    long RateLimitedRequests,
    long RateLimitedUpgrades,
    long RequestBodySizeRejections,
    long ParserLimitRejections,
    IReadOnlyDictionary<string, long> RequestFailuresByKind,
    IReadOnlyList<ProxyRequestSeriesSnapshot> RequestsByRoute,
    long ConfigReloadSuccesses,
    long ConfigReloadFailures,
    long AdminAuthSuccesses,
    long AdminAuthFailures,
    long AcmeRenewalAttempts,
    long AcmeRenewalSuccesses,
    long AcmeRenewalFailures,
    long RetryAttempts,
    long RetryExhausted,
    IReadOnlyList<ProxyRetrySkippedSnapshot> RetrySkipped,
    long CircuitOpened,
    long CircuitHalfOpened,
    long CircuitClosed,
    long CircuitRejections,
    long NoAvailableUpstreamFailures,
    IReadOnlyList<ProxyUpstreamSelectionSnapshot> UpstreamSelectionsByUpstream,
    long ListenerReloadAttempts,
    long ListenerReloadSuccesses,
    long ListenerReloadFailures,
    long ListenerReloadAdded,
    long ListenerReloadRemoved,
    long ListenerReloadChanged,
    long ListenerReloadUnchanged,
    long ListenerStartFailures,
    long ListenerDrainCount,
    long ActiveListeners,
    long Http2AcceptedConnections,
    long Http2Requests,
    long ActiveHttp2Streams,
    IReadOnlyDictionary<string, long> Http2ProtocolErrors,
    long UpstreamHttp2Requests,
    long UpstreamHttp2AlpnFailures,
    long UpstreamHttp2ProtocolErrors,
    long UpstreamHttp3Requests,
    long UpstreamHttp3ConnectionAttempts,
    long UpstreamHttp3ConnectionSuccesses,
    long UpstreamHttp3ConnectionFailures,
    long UpstreamHttp3PoolConnectionsOpened,
    long UpstreamHttp3PoolConnectionsReused,
    long UpstreamHttp3PoolConnectionsClosed,
    long UpstreamHttp3StreamLimitRejections,
    long ActiveUpstreamHttp3Connections,
    long ActiveUpstreamHttp3Streams,
    IReadOnlyDictionary<string, long> UpstreamHttp3ProtocolErrors,
    ProxyHttp3MetricsSnapshot Http3,
    long ConfigLintRuns,
    IReadOnlyList<ProxyConfigLintFindingMetricSnapshot> ConfigLintFindings,
    long RouteMatchDryRuns,
    IReadOnlyList<ProxyRouteDryRunFailureSnapshot> RouteMatchDryRunFailures)
{
    public ProxyHttp3MetricsSnapshot Http3 { get; } =
        Http3 ?? throw new ArgumentNullException(nameof(Http3));

    public IReadOnlyDictionary<string, long> RequestFailuresByKind { get; } =
        MetricsList.CopyDictionary(RequestFailuresByKind, StringComparer.Ordinal);

    public IReadOnlyList<ProxyRequestSeriesSnapshot> RequestsByRoute { get; } =
        MetricsList.Copy(RequestsByRoute);

    public IReadOnlyList<ProxyRetrySkippedSnapshot> RetrySkipped { get; } =
        MetricsList.Copy(RetrySkipped);

    public IReadOnlyList<ProxyUpstreamSelectionSnapshot> UpstreamSelectionsByUpstream { get; } =
        MetricsList.Copy(UpstreamSelectionsByUpstream);

    public IReadOnlyDictionary<string, long> Http2ProtocolErrors { get; } =
        MetricsList.CopyDictionary(Http2ProtocolErrors, StringComparer.Ordinal);

    public IReadOnlyDictionary<string, long> UpstreamHttp3ProtocolErrors { get; } =
        MetricsList.CopyDictionary(UpstreamHttp3ProtocolErrors, StringComparer.Ordinal);

    public IReadOnlyList<ProxyConfigLintFindingMetricSnapshot> ConfigLintFindings { get; } =
        MetricsList.Copy(ConfigLintFindings);

    public IReadOnlyList<ProxyRouteDryRunFailureSnapshot> RouteMatchDryRunFailures { get; } =
        MetricsList.Copy(RouteMatchDryRunFailures);
}
