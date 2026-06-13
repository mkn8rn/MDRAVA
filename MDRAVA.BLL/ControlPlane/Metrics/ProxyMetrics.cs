using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using MDRAVA.BLL.ControlPlane.Status;
using System.Collections.Concurrent;
using MDRAVA.BLL.ControlPlane.Forwarding;
using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.AdminAuthentication;
using MDRAVA.BLL.ControlPlane.ConfigLint;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.RequestDiagnostics;
using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.BLL.ControlPlane.RouteDiagnostics;
using MDRAVA.BLL.ControlPlane.RuntimeGuards;
using MDRAVA.BLL.ControlPlane.UpstreamSelection;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class ProxyMetrics :
    IProxyStatusMetricsSource,
    IProxyUpstreamSelectionMetricsSink,
    IProxyCircuitBreakerMetricsSink,
    IProxyUpstreamHealthMetricsSink,
    IProxyHttp3AltSvcMetricsSink,
    IProxyAccessLogMetricsSink,
    IProxyRequestDiagnosticsMetricsSink,
    IProxyRequestIdMetricsSink,
    IProxyRateLimitMetricsSink,
    IProxyAdmissionMetricsSink,
    IProxyConfigurationReloadMetricsSink,
    IProxyHealthCheckMetricsSink,
    IProxyAcmeMetricsSink,
    IProxyAdminAuthenticationMetricsSink,
    IProxyConfigLintMetricsSink,
    IProxyRouteDiagnosticsMetricsSink
{
    private static readonly ProxyFailureKind[] FailureKinds = Enum.GetValues<ProxyFailureKind>();

    private long _acceptedConnections;
    private long _activeConnections;
    private long _totalRequests;
    private long _upstreamSuccesses;
    private long _upstreamFailures;
    private long _bytesRead;
    private long _bytesWritten;
    private long _parseErrors;
    private long _rejectedMalformedRequests;
    private long _rejectedUnsupportedRequestFraming;
    private long _upstreamMalformedResponses;
    private long _clientBodyRelayFailures;
    private long _upstreamBodyRelayFailures;
    private long _clientRequestHeadTimeouts;
    private long _clientRequestBodyTimeouts;
    private long _upstreamConnectFailures;
    private long _upstreamConnectTimeouts;
    private long _upstreamResponseHeadTimeouts;
    private long _upstreamResponseBodyTimeouts;
    private long _upstreamPrematureDisconnects;
    private long _clientPrematureDisconnects;
    private long _proxyGenerated502Responses;
    private long _proxyGenerated504Responses;
    private long _downstreamWriteTimeouts;
    private long _tlsHandshakeAttempts;
    private long _tlsHandshakeSuccesses;
    private long _tlsHandshakeFailures;
    private long _tlsHandshakeTimeouts;
    private long _tlsNoCertificateForSniFailures;
    private long _clientConnectionsClosedByIdleTimeout;
    private long _clientConnectionsClosedByMaxRequests;
    private long _upstreamConnectionsOpened;
    private long _upstreamConnectionsReused;
    private long _upstreamConnectionsDiscarded;
    private long _upstreamPoolIdleConnections;
    private long _upstreamPoolActiveConnections;
    private long _upgradeRequestsReceived;
    private long _upgradeRequestsSucceeded;
    private long _upgradeRequestsRejected;
    private long _upgradeUpstreamFailures;
    private long _activeTunnels;
    private long _totalTunnels;
    private long _tunnelIdleTimeouts;
    private long _tunnelBytesClientToUpstream;
    private long _tunnelBytesUpstreamToClient;
    private long _tunnelRelayFailures;
    private long _upstreamSelections;
    private long _noHealthyUpstreamFailures;
    private long _healthChecksAttempted;
    private long _healthChecksSucceeded;
    private long _healthChecksFailed;
    private long _upstreamHealthTransitions;
    private long _upstreamRequestFailures;
    private long _requestIdsGenerated;
    private long _accessLogsEmitted;
    private long _recentDiagnosticsOverwritten;
    private long _connectionAdmissionRejections;
    private long _activeTlsHandshakes;
    private long _tlsHandshakeAdmissionRejections;
    private long _rateLimitedRequests;
    private long _rateLimitedUpgrades;
    private long _requestBodySizeRejections;
    private long _parserLimitRejections;
    private long _configReloadSuccesses;
    private long _configReloadFailures;
    private long _adminAuthSuccesses;
    private long _adminAuthFailures;
    private long _acmeRenewalAttempts;
    private long _acmeRenewalSuccesses;
    private long _acmeRenewalFailures;
    private long _retryAttempts;
    private long _retryExhausted;
    private long _circuitOpened;
    private long _circuitHalfOpened;
    private long _circuitClosed;
    private long _circuitRejections;
    private long _noAvailableUpstreamFailures;
    private long _listenerReloadAttempts;
    private long _listenerReloadSuccesses;
    private long _listenerReloadFailures;
    private long _listenerReloadAdded;
    private long _listenerReloadRemoved;
    private long _listenerReloadChanged;
    private long _listenerReloadUnchanged;
    private long _listenerStartFailures;
    private long _listenerDrainCount;
    private long _activeListeners;
    private long _http2AcceptedConnections;
    private long _http2Requests;
    private long _activeHttp2Streams;
    private long _upstreamHttp2Requests;
    private long _upstreamHttp2AlpnFailures;
    private long _upstreamHttp2ProtocolErrors;
    private long _upstreamHttp3Requests;
    private long _upstreamHttp3ConnectionAttempts;
    private long _upstreamHttp3ConnectionSuccesses;
    private long _upstreamHttp3ConnectionFailures;
    private long _upstreamHttp3PoolConnectionsOpened;
    private long _upstreamHttp3PoolConnectionsReused;
    private long _upstreamHttp3PoolConnectionsClosed;
    private long _upstreamHttp3StreamLimitRejections;
    private long _activeUpstreamHttp3Connections;
    private long _activeUpstreamHttp3Streams;
    private long _http3AcceptedConnections;
    private long _activeHttp3Connections;
    private long _http3Requests;
    private long _http3ProxiedRequests;
    private long _http3GeneratedResponses;
    private long _activeHttp3Streams;
    private long _http3StreamResets;
    private long _http3StreamedResponses;
    private long _activeHttp3ResponseStreams;
    private long _http3ResponseBytesSent;
    private long _http3RequestBodyBytesReceived;
    private long _http3ResponseStreamResets;
    private long _http3AltSvcEmitted;
    private long _http3AltSvcSuppressed;
    private long _quicListenerStartSuccesses;
    private long _quicListenerStartFailures;
    private long _activeQuicListeners;
    private long _configLintRuns;
    private long _routeMatchDryRuns;
    private readonly long[] _requestFailuresByKind = new long[FailureKinds.Length];
    private readonly ConcurrentDictionary<RequestSeriesKey, RequestSeriesCounter> _requestsByRoute = new();
    private readonly ConcurrentDictionary<string, RequestSeriesCounter> _retrySkippedByReason = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<UpstreamSelectionKey, RequestSeriesCounter> _upstreamSelectionsByUpstream = new();
    private readonly ConcurrentDictionary<string, RequestSeriesCounter> _http2ProtocolErrors = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, RequestSeriesCounter> _upstreamHttp3ProtocolErrors = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Http3OutcomeKey, RequestSeriesCounter> _http3RequestsByOutcome = new();
    private readonly ConcurrentDictionary<string, RequestSeriesCounter> _http3RejectedRequests = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, RequestSeriesCounter> _http3ProtocolErrors = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<ConfigLintFindingKey, RequestSeriesCounter> _configLintFindings = new();
    private readonly ConcurrentDictionary<string, RequestSeriesCounter> _routeMatchDryRunFailures = new(StringComparer.Ordinal);

}
