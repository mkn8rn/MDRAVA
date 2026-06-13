using MDRAVA.BLL.ControlPlane.ConfigLint;
using MDRAVA.BLL.ControlPlane.Forwarding;
using MDRAVA.BLL.ControlPlane.RequestDiagnostics;
using MDRAVA.BLL.ControlPlane.RouteDiagnostics;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class ProxyMetrics
{
    public ProxyMetricsSnapshot Snapshot()
    {
        Dictionary<string, long> failuresByKind = new(StringComparer.Ordinal);
        foreach (var failureKind in FailureKinds)
        {
            if (failureKind == ProxyFailureKind.None)
            {
                continue;
            }

            failuresByKind[failureKind.ToString()] = Interlocked.Read(ref _requestFailuresByKind[(int)failureKind]);
        }

        var requestsByRoute = _requestsByRoute
            .Select(static pair => new ProxyRequestSeriesSnapshot(
                pair.Key.Site,
                pair.Key.Route,
                pair.Key.Action,
                pair.Key.StatusClass,
                Interlocked.Read(ref pair.Value.Count)))
            .OrderBy(static item => item.Site, StringComparer.Ordinal)
            .ThenBy(static item => item.Route, StringComparer.Ordinal)
            .ThenBy(static item => item.Action, StringComparer.Ordinal)
            .ThenBy(static item => item.StatusClass, StringComparer.Ordinal)
            .ToArray();
        var retrySkipped = _retrySkippedByReason
            .Select(static pair => new ProxyRetrySkippedSnapshot(pair.Key, Interlocked.Read(ref pair.Value.Count)))
            .OrderBy(static item => item.Reason, StringComparer.Ordinal)
            .ToArray();
        var upstreamSelectionsByUpstream = _upstreamSelectionsByUpstream
            .Select(static pair => new ProxyUpstreamSelectionSnapshot(
                pair.Key.Route,
                pair.Key.Upstream,
                pair.Key.Scheme,
                pair.Key.Protocol,
                Interlocked.Read(ref pair.Value.Count)))
            .OrderBy(static item => item.Route, StringComparer.Ordinal)
            .ThenBy(static item => item.Upstream, StringComparer.Ordinal)
            .ThenBy(static item => item.Scheme, StringComparer.Ordinal)
            .ToArray();
        var http2ProtocolErrors = _http2ProtocolErrors
            .ToDictionary(static pair => pair.Key, static pair => Interlocked.Read(ref pair.Value.Count), StringComparer.Ordinal);
        var upstreamHttp3ProtocolErrors = _upstreamHttp3ProtocolErrors
            .ToDictionary(static pair => pair.Key, static pair => Interlocked.Read(ref pair.Value.Count), StringComparer.Ordinal);
        var http3RequestsByOutcome = _http3RequestsByOutcome
            .Select(static pair => new ProxyHttp3RequestOutcomeSnapshot(
                pair.Key.Method,
                pair.Key.Outcome,
                pair.Key.StatusClass,
                Interlocked.Read(ref pair.Value.Count)))
            .OrderBy(static item => item.Method, StringComparer.Ordinal)
            .ThenBy(static item => item.Outcome, StringComparer.Ordinal)
            .ThenBy(static item => item.StatusClass, StringComparer.Ordinal)
            .ToArray();
        var http3RejectedRequests = _http3RejectedRequests
            .ToDictionary(static pair => pair.Key, static pair => Interlocked.Read(ref pair.Value.Count), StringComparer.Ordinal);
        var http3ProtocolErrors = _http3ProtocolErrors
            .ToDictionary(static pair => pair.Key, static pair => Interlocked.Read(ref pair.Value.Count), StringComparer.Ordinal);
        var configLintFindings = _configLintFindings
            .Select(static pair => new ProxyConfigLintFindingMetricSnapshot(
                pair.Key.Severity,
                pair.Key.Code,
                Interlocked.Read(ref pair.Value.Count)))
            .OrderBy(static item => item.Severity, StringComparer.Ordinal)
            .ThenBy(static item => item.Code, StringComparer.Ordinal)
            .ToArray();
        var routeMatchDryRunFailures = _routeMatchDryRunFailures
            .Select(static pair => new ProxyRouteDryRunFailureSnapshot(pair.Key, Interlocked.Read(ref pair.Value.Count)))
            .OrderBy(static item => item.Reason, StringComparer.Ordinal)
            .ToArray();

        return new ProxyMetricsSnapshot(
            Interlocked.Read(ref _acceptedConnections),
            Interlocked.Read(ref _activeConnections),
            Interlocked.Read(ref _totalRequests),
            Interlocked.Read(ref _upstreamSuccesses),
            Interlocked.Read(ref _upstreamFailures),
            Interlocked.Read(ref _bytesRead),
            Interlocked.Read(ref _bytesWritten),
            Interlocked.Read(ref _parseErrors),
            Interlocked.Read(ref _rejectedMalformedRequests),
            Interlocked.Read(ref _rejectedUnsupportedRequestFraming),
            Interlocked.Read(ref _upstreamMalformedResponses),
            Interlocked.Read(ref _clientBodyRelayFailures),
            Interlocked.Read(ref _upstreamBodyRelayFailures),
            Interlocked.Read(ref _clientRequestHeadTimeouts),
            Interlocked.Read(ref _clientRequestBodyTimeouts),
            Interlocked.Read(ref _upstreamConnectFailures),
            Interlocked.Read(ref _upstreamConnectTimeouts),
            Interlocked.Read(ref _upstreamResponseHeadTimeouts),
            Interlocked.Read(ref _upstreamResponseBodyTimeouts),
            Interlocked.Read(ref _upstreamPrematureDisconnects),
            Interlocked.Read(ref _clientPrematureDisconnects),
            Interlocked.Read(ref _proxyGenerated502Responses),
            Interlocked.Read(ref _proxyGenerated504Responses),
            Interlocked.Read(ref _downstreamWriteTimeouts),
            Interlocked.Read(ref _tlsHandshakeAttempts),
            Interlocked.Read(ref _tlsHandshakeSuccesses),
            Interlocked.Read(ref _tlsHandshakeFailures),
            Interlocked.Read(ref _tlsHandshakeTimeouts),
            Interlocked.Read(ref _tlsNoCertificateForSniFailures),
            Interlocked.Read(ref _clientConnectionsClosedByIdleTimeout),
            Interlocked.Read(ref _clientConnectionsClosedByMaxRequests),
            Interlocked.Read(ref _upstreamConnectionsOpened),
            Interlocked.Read(ref _upstreamConnectionsReused),
            Interlocked.Read(ref _upstreamConnectionsDiscarded),
            Interlocked.Read(ref _upstreamPoolIdleConnections),
            Interlocked.Read(ref _upstreamPoolActiveConnections),
            Interlocked.Read(ref _upgradeRequestsReceived),
            Interlocked.Read(ref _upgradeRequestsSucceeded),
            Interlocked.Read(ref _upgradeRequestsRejected),
            Interlocked.Read(ref _upgradeUpstreamFailures),
            Interlocked.Read(ref _activeTunnels),
            Interlocked.Read(ref _totalTunnels),
            Interlocked.Read(ref _tunnelIdleTimeouts),
            Interlocked.Read(ref _tunnelBytesClientToUpstream),
            Interlocked.Read(ref _tunnelBytesUpstreamToClient),
            Interlocked.Read(ref _tunnelRelayFailures),
            Interlocked.Read(ref _upstreamSelections),
            Interlocked.Read(ref _noHealthyUpstreamFailures),
            Interlocked.Read(ref _healthChecksAttempted),
            Interlocked.Read(ref _healthChecksSucceeded),
            Interlocked.Read(ref _healthChecksFailed),
            Interlocked.Read(ref _upstreamHealthTransitions),
            Interlocked.Read(ref _upstreamRequestFailures),
            Interlocked.Read(ref _requestIdsGenerated),
            Interlocked.Read(ref _accessLogsEmitted),
            Interlocked.Read(ref _recentDiagnosticsOverwritten),
            Interlocked.Read(ref _connectionAdmissionRejections),
            Interlocked.Read(ref _activeTlsHandshakes),
            Interlocked.Read(ref _tlsHandshakeAdmissionRejections),
            Interlocked.Read(ref _rateLimitedRequests),
            Interlocked.Read(ref _rateLimitedUpgrades),
            Interlocked.Read(ref _requestBodySizeRejections),
            Interlocked.Read(ref _parserLimitRejections),
            failuresByKind,
            requestsByRoute,
            Interlocked.Read(ref _configReloadSuccesses),
            Interlocked.Read(ref _configReloadFailures),
            Interlocked.Read(ref _adminAuthSuccesses),
            Interlocked.Read(ref _adminAuthFailures),
            Interlocked.Read(ref _acmeRenewalAttempts),
            Interlocked.Read(ref _acmeRenewalSuccesses),
            Interlocked.Read(ref _acmeRenewalFailures),
            Interlocked.Read(ref _retryAttempts),
            Interlocked.Read(ref _retryExhausted),
            retrySkipped,
            Interlocked.Read(ref _circuitOpened),
            Interlocked.Read(ref _circuitHalfOpened),
            Interlocked.Read(ref _circuitClosed),
            Interlocked.Read(ref _circuitRejections),
            Interlocked.Read(ref _noAvailableUpstreamFailures),
            upstreamSelectionsByUpstream,
            Interlocked.Read(ref _listenerReloadAttempts),
            Interlocked.Read(ref _listenerReloadSuccesses),
            Interlocked.Read(ref _listenerReloadFailures),
            Interlocked.Read(ref _listenerReloadAdded),
            Interlocked.Read(ref _listenerReloadRemoved),
            Interlocked.Read(ref _listenerReloadChanged),
            Interlocked.Read(ref _listenerReloadUnchanged),
            Interlocked.Read(ref _listenerStartFailures),
            Interlocked.Read(ref _listenerDrainCount),
            Interlocked.Read(ref _activeListeners),
            Interlocked.Read(ref _http2AcceptedConnections),
            Interlocked.Read(ref _http2Requests),
            Interlocked.Read(ref _activeHttp2Streams),
            http2ProtocolErrors,
            Interlocked.Read(ref _upstreamHttp2Requests),
            Interlocked.Read(ref _upstreamHttp2AlpnFailures),
            Interlocked.Read(ref _upstreamHttp2ProtocolErrors),
            Interlocked.Read(ref _upstreamHttp3Requests),
            Interlocked.Read(ref _upstreamHttp3ConnectionAttempts),
            Interlocked.Read(ref _upstreamHttp3ConnectionSuccesses),
            Interlocked.Read(ref _upstreamHttp3ConnectionFailures),
            Interlocked.Read(ref _upstreamHttp3PoolConnectionsOpened),
            Interlocked.Read(ref _upstreamHttp3PoolConnectionsReused),
            Interlocked.Read(ref _upstreamHttp3PoolConnectionsClosed),
            Interlocked.Read(ref _upstreamHttp3StreamLimitRejections),
            Interlocked.Read(ref _activeUpstreamHttp3Connections),
            Interlocked.Read(ref _activeUpstreamHttp3Streams),
            upstreamHttp3ProtocolErrors,
            Interlocked.Read(ref _http3AcceptedConnections),
            Interlocked.Read(ref _activeHttp3Connections),
            Interlocked.Read(ref _http3Requests),
            Interlocked.Read(ref _http3ProxiedRequests),
            Interlocked.Read(ref _http3GeneratedResponses),
            Interlocked.Read(ref _activeHttp3Streams),
            Interlocked.Read(ref _http3StreamResets),
            Interlocked.Read(ref _http3StreamedResponses),
            Interlocked.Read(ref _activeHttp3ResponseStreams),
            Interlocked.Read(ref _http3ResponseBytesSent),
            Interlocked.Read(ref _http3RequestBodyBytesReceived),
            Interlocked.Read(ref _http3ResponseStreamResets),
            Interlocked.Read(ref _http3AltSvcEmitted),
            Interlocked.Read(ref _http3AltSvcSuppressed),
            http3RequestsByOutcome,
            http3RejectedRequests,
            http3ProtocolErrors,
            Interlocked.Read(ref _quicListenerStartSuccesses),
            Interlocked.Read(ref _quicListenerStartFailures),
            Interlocked.Read(ref _activeQuicListeners),
            Interlocked.Read(ref _configLintRuns),
            configLintFindings,
            Interlocked.Read(ref _routeMatchDryRuns),
            routeMatchDryRunFailures);
    }

    public ProxyMetricsSnapshot ReadMetrics()
    {
        return Snapshot();
    }
}
