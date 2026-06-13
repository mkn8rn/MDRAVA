namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class ProxyMetrics
{
    public ProxyMetricsSnapshot Snapshot()
    {
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
            ReadTlsMetricsSnapshot(),
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
            ReadTunnelMetricsSnapshot(),
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
            Interlocked.Read(ref _rateLimitedRequests),
            Interlocked.Read(ref _rateLimitedUpgrades),
            Interlocked.Read(ref _requestBodySizeRejections),
            Interlocked.Read(ref _parserLimitRejections),
            ReadRequestFailuresByKind(),
            ReadRequestsByRoute(),
            Interlocked.Read(ref _configReloadSuccesses),
            Interlocked.Read(ref _configReloadFailures),
            Interlocked.Read(ref _adminAuthSuccesses),
            Interlocked.Read(ref _adminAuthFailures),
            Interlocked.Read(ref _acmeRenewalAttempts),
            Interlocked.Read(ref _acmeRenewalSuccesses),
            Interlocked.Read(ref _acmeRenewalFailures),
            ReadResilienceMetricsSnapshot(),
            ReadUpstreamSelectionsByUpstream(),
            ReadListenerMetricsSnapshot(),
            Interlocked.Read(ref _http2AcceptedConnections),
            Interlocked.Read(ref _http2Requests),
            Interlocked.Read(ref _activeHttp2Streams),
            ReadHttp2ProtocolErrors(),
            Interlocked.Read(ref _upstreamHttp2Requests),
            Interlocked.Read(ref _upstreamHttp2AlpnFailures),
            Interlocked.Read(ref _upstreamHttp2ProtocolErrors),
            ReadUpstreamHttp3MetricsSnapshot(),
            ReadHttp3MetricsSnapshot(),
            Interlocked.Read(ref _configLintRuns),
            ReadConfigLintFindings(),
            Interlocked.Read(ref _routeMatchDryRuns),
            ReadRouteMatchDryRunFailures());
    }

    private ProxyHttp3MetricsSnapshot ReadHttp3MetricsSnapshot()
    {
        return new ProxyHttp3MetricsSnapshot(
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
            ReadHttp3RequestsByOutcome(),
            ReadHttp3RejectedRequests(),
            ReadHttp3ProtocolErrors(),
            Interlocked.Read(ref _quicListenerStartSuccesses),
            Interlocked.Read(ref _quicListenerStartFailures),
            Interlocked.Read(ref _activeQuicListeners));
    }

    private ProxyListenerMetricsSnapshot ReadListenerMetricsSnapshot()
    {
        return new ProxyListenerMetricsSnapshot(
            Interlocked.Read(ref _listenerReloadAttempts),
            Interlocked.Read(ref _listenerReloadSuccesses),
            Interlocked.Read(ref _listenerReloadFailures),
            Interlocked.Read(ref _listenerReloadAdded),
            Interlocked.Read(ref _listenerReloadRemoved),
            Interlocked.Read(ref _listenerReloadChanged),
            Interlocked.Read(ref _listenerReloadUnchanged),
            Interlocked.Read(ref _listenerStartFailures),
            Interlocked.Read(ref _listenerDrainCount),
            Interlocked.Read(ref _activeListeners));
    }

    private ProxyTunnelMetricsSnapshot ReadTunnelMetricsSnapshot()
    {
        return new ProxyTunnelMetricsSnapshot(
            Interlocked.Read(ref _activeTunnels),
            Interlocked.Read(ref _totalTunnels),
            Interlocked.Read(ref _tunnelIdleTimeouts),
            Interlocked.Read(ref _tunnelBytesClientToUpstream),
            Interlocked.Read(ref _tunnelBytesUpstreamToClient),
            Interlocked.Read(ref _tunnelRelayFailures));
    }

    private ProxyTlsMetricsSnapshot ReadTlsMetricsSnapshot()
    {
        return new ProxyTlsMetricsSnapshot(
            Interlocked.Read(ref _tlsHandshakeAttempts),
            Interlocked.Read(ref _tlsHandshakeSuccesses),
            Interlocked.Read(ref _tlsHandshakeFailures),
            Interlocked.Read(ref _tlsHandshakeTimeouts),
            Interlocked.Read(ref _tlsNoCertificateForSniFailures),
            Interlocked.Read(ref _activeTlsHandshakes),
            Interlocked.Read(ref _tlsHandshakeAdmissionRejections));
    }

    private ProxyResilienceMetricsSnapshot ReadResilienceMetricsSnapshot()
    {
        return new ProxyResilienceMetricsSnapshot(
            Interlocked.Read(ref _retryAttempts),
            Interlocked.Read(ref _retryExhausted),
            ReadRetrySkipped(),
            Interlocked.Read(ref _circuitOpened),
            Interlocked.Read(ref _circuitHalfOpened),
            Interlocked.Read(ref _circuitClosed),
            Interlocked.Read(ref _circuitRejections),
            Interlocked.Read(ref _noAvailableUpstreamFailures));
    }

    private ProxyUpstreamHttp3MetricsSnapshot ReadUpstreamHttp3MetricsSnapshot()
    {
        return new ProxyUpstreamHttp3MetricsSnapshot(
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
            ReadUpstreamHttp3ProtocolErrors());
    }

    public ProxyMetricsSnapshot ReadMetrics()
    {
        return Snapshot();
    }
}
