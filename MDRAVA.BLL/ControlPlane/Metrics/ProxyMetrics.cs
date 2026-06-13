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

    public void ConnectionAccepted()
    {
        Interlocked.Increment(ref _acceptedConnections);
        Interlocked.Increment(ref _activeConnections);
    }

    public void ConnectionClosed()
    {
        Interlocked.Decrement(ref _activeConnections);
    }

    public void RequestReceived()
    {
        Interlocked.Increment(ref _totalRequests);
    }

    public void UpstreamSucceeded()
    {
        Interlocked.Increment(ref _upstreamSuccesses);
    }

    public void UpstreamFailed()
    {
        Interlocked.Increment(ref _upstreamFailures);
    }

    public void UpstreamConnectFailed()
    {
        Interlocked.Increment(ref _upstreamConnectFailures);
    }

    public void AddBytesRead(long bytes)
    {
        if (bytes > 0)
        {
            Interlocked.Add(ref _bytesRead, bytes);
        }
    }

    public void AddBytesWritten(long bytes)
    {
        if (bytes > 0)
        {
            Interlocked.Add(ref _bytesWritten, bytes);
        }
    }

    public void ParseFailed()
    {
        Interlocked.Increment(ref _parseErrors);
    }

    public void MalformedRequestRejected()
    {
        Interlocked.Increment(ref _rejectedMalformedRequests);
    }

    public void UnsupportedRequestFramingRejected()
    {
        Interlocked.Increment(ref _rejectedUnsupportedRequestFraming);
    }

    public void UpstreamMalformedResponse()
    {
        Interlocked.Increment(ref _upstreamMalformedResponses);
    }

    public void ClientBodyRelayFailed()
    {
        Interlocked.Increment(ref _clientBodyRelayFailures);
    }

    public void UpstreamBodyRelayFailed()
    {
        Interlocked.Increment(ref _upstreamBodyRelayFailures);
    }

    public void ClientRequestHeadTimedOut() => Interlocked.Increment(ref _clientRequestHeadTimeouts);

    public void ClientRequestBodyTimedOut() => Interlocked.Increment(ref _clientRequestBodyTimeouts);

    public void UpstreamConnectTimedOut() => Interlocked.Increment(ref _upstreamConnectTimeouts);

    public void UpstreamResponseHeadTimedOut() => Interlocked.Increment(ref _upstreamResponseHeadTimeouts);

    public void UpstreamResponseBodyTimedOut() => Interlocked.Increment(ref _upstreamResponseBodyTimeouts);

    public void UpstreamPrematureDisconnect() => Interlocked.Increment(ref _upstreamPrematureDisconnects);

    public void ClientPrematureDisconnect() => Interlocked.Increment(ref _clientPrematureDisconnects);

    public void ProxyGenerated502() => Interlocked.Increment(ref _proxyGenerated502Responses);

    public void ProxyGenerated504() => Interlocked.Increment(ref _proxyGenerated504Responses);

    public void DownstreamWriteTimedOut() => Interlocked.Increment(ref _downstreamWriteTimeouts);

    public void TlsHandshakeAttempted() => Interlocked.Increment(ref _tlsHandshakeAttempts);

    public void TlsHandshakeSucceeded() => Interlocked.Increment(ref _tlsHandshakeSuccesses);

    public void TlsHandshakeFailed() => Interlocked.Increment(ref _tlsHandshakeFailures);

    public void TlsHandshakeTimedOut() => Interlocked.Increment(ref _tlsHandshakeTimeouts);

    public void TlsNoCertificateForSni() => Interlocked.Increment(ref _tlsNoCertificateForSniFailures);

    public void ClientConnectionClosedByIdleTimeout() => Interlocked.Increment(ref _clientConnectionsClosedByIdleTimeout);

    public void ClientConnectionClosedByMaxRequests() => Interlocked.Increment(ref _clientConnectionsClosedByMaxRequests);

    public void UpstreamConnectionOpened() => Interlocked.Increment(ref _upstreamConnectionsOpened);

    public void UpstreamConnectionReused() => Interlocked.Increment(ref _upstreamConnectionsReused);

    public void UpstreamConnectionDiscarded() => Interlocked.Increment(ref _upstreamConnectionsDiscarded);

    public void UpstreamPoolConnectionBorrowed()
    {
        Interlocked.Increment(ref _upstreamPoolActiveConnections);
    }

    public void UpstreamPoolConnectionReturnedIdle()
    {
        Interlocked.Decrement(ref _upstreamPoolActiveConnections);
        Interlocked.Increment(ref _upstreamPoolIdleConnections);
    }

    public void UpstreamPoolConnectionReusedFromIdle()
    {
        Interlocked.Decrement(ref _upstreamPoolIdleConnections);
        Interlocked.Increment(ref _upstreamPoolActiveConnections);
    }

    public void UpstreamPoolConnectionClosedActive()
    {
        Interlocked.Decrement(ref _upstreamPoolActiveConnections);
    }

    public void UpstreamPoolIdleConnectionDiscarded()
    {
        Interlocked.Decrement(ref _upstreamPoolIdleConnections);
    }

    public void UpgradeRequestReceived() => Interlocked.Increment(ref _upgradeRequestsReceived);

    public void UpgradeRequestSucceeded() => Interlocked.Increment(ref _upgradeRequestsSucceeded);

    public void UpgradeRequestRejected() => Interlocked.Increment(ref _upgradeRequestsRejected);

    public void UpgradeUpstreamFailed() => Interlocked.Increment(ref _upgradeUpstreamFailures);

    public ProxyTunnelAdmissionDecision StartTunnel(int maxActiveTunnels)
    {
        while (true)
        {
            var observed = Interlocked.Read(ref _activeTunnels);
            if (observed >= maxActiveTunnels)
            {
                return ProxyTunnelAdmissionDecision.Rejected;
            }

            if (Interlocked.CompareExchange(ref _activeTunnels, observed + 1, observed) == observed)
            {
                return ProxyTunnelAdmissionDecision.Accepted;
            }
        }
    }

    public void TunnelStarted() => Interlocked.Increment(ref _totalTunnels);

    public void TunnelClosed() => Interlocked.Decrement(ref _activeTunnels);

    public void TunnelIdleTimedOut() => Interlocked.Increment(ref _tunnelIdleTimeouts);

    public void AddTunnelBytesClientToUpstream(long bytes)
    {
        if (bytes > 0)
        {
            Interlocked.Add(ref _tunnelBytesClientToUpstream, bytes);
        }
    }

    public void AddTunnelBytesUpstreamToClient(long bytes)
    {
        if (bytes > 0)
        {
            Interlocked.Add(ref _tunnelBytesUpstreamToClient, bytes);
        }
    }

    public void TunnelRelayFailed() => Interlocked.Increment(ref _tunnelRelayFailures);

    public void UpstreamSelected(ProxyUpstreamSelectionMetric selection)
    {
        Interlocked.Increment(ref _upstreamSelections);
        var key = new UpstreamSelectionKey(
            ProxyMetricLabelPolicy.NormalizeValue(selection.Route),
            ProxyMetricLabelPolicy.NormalizeValue(selection.Upstream),
            ProxyMetricLabelPolicy.NormalizeValue(selection.Scheme),
            ProxyMetricLabelPolicy.NormalizeValue(selection.Protocol));
        var counter = _upstreamSelectionsByUpstream.GetOrAdd(key, static _ => new RequestSeriesCounter());
        Interlocked.Increment(ref counter.Count);
    }

    public void NoHealthyUpstream() => Interlocked.Increment(ref _noHealthyUpstreamFailures);

    public void NoAvailableUpstream() => Interlocked.Increment(ref _noAvailableUpstreamFailures);

    public void HealthCheckAttempted() => Interlocked.Increment(ref _healthChecksAttempted);

    public void HealthCheckSucceeded() => Interlocked.Increment(ref _healthChecksSucceeded);

    public void HealthCheckFailed() => Interlocked.Increment(ref _healthChecksFailed);

    public void UpstreamHealthTransition() => Interlocked.Increment(ref _upstreamHealthTransitions);

    public void UpstreamRequestFailed() => Interlocked.Increment(ref _upstreamRequestFailures);

    public void RequestIdGenerated() => Interlocked.Increment(ref _requestIdsGenerated);

    public void AccessLogEmitted() => Interlocked.Increment(ref _accessLogsEmitted);

    public void RecentDiagnosticOverwritten() => Interlocked.Increment(ref _recentDiagnosticsOverwritten);

    public void RequestFailed(ProxyFailureKind failureKind)
    {
        if (failureKind == ProxyFailureKind.None)
        {
            return;
        }

        var index = (int)failureKind;
        if ((uint)index < (uint)_requestFailuresByKind.Length)
        {
            Interlocked.Increment(ref _requestFailuresByKind[index]);
        }
    }

    public void ConnectionAdmissionRejected() => Interlocked.Increment(ref _connectionAdmissionRejections);

    public void TlsHandshakeStarted() => Interlocked.Increment(ref _activeTlsHandshakes);

    public void TlsHandshakeEnded() => Interlocked.Decrement(ref _activeTlsHandshakes);

    public void TlsHandshakeAdmissionRejected() => Interlocked.Increment(ref _tlsHandshakeAdmissionRejections);

    public void RequestRateLimited() => Interlocked.Increment(ref _rateLimitedRequests);

    public void UpgradeRateLimited() => Interlocked.Increment(ref _rateLimitedUpgrades);

    public void RequestBodySizeRejected() => Interlocked.Increment(ref _requestBodySizeRejections);

    public void ParserLimitRejected() => Interlocked.Increment(ref _parserLimitRejections);

    public void RequestCompleted(string? site, string? route, string? action, int? statusCode)
    {
        var key = new RequestSeriesKey(
            ProxyMetricLabelPolicy.NormalizeValue(site),
            ProxyMetricLabelPolicy.NormalizeValue(route),
            ProxyMetricLabelPolicy.NormalizeValue(action),
            ProxyMetricLabelPolicy.StatusClass(statusCode));
        var counter = _requestsByRoute.GetOrAdd(key, static _ => new RequestSeriesCounter());
        Interlocked.Increment(ref counter.Count);
    }

}
