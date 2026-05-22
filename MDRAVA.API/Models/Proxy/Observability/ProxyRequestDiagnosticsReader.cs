namespace MDRAVA.API.Proxy.Observability;

public sealed class ProxyRequestDiagnosticsReader : IProxyRequestDiagnosticsReader
{
    private readonly RecentRequestDiagnosticsStore _diagnosticsStore;

    public ProxyRequestDiagnosticsReader(RecentRequestDiagnosticsStore diagnosticsStore)
    {
        _diagnosticsStore = diagnosticsStore;
    }

    public IReadOnlyList<ProxyRecentRequestDiagnosticEvent> Recent(int limit)
    {
        return _diagnosticsStore.Recent(limit)
            .Select(static diagnostic => new ProxyRecentRequestDiagnosticEvent(
                diagnostic.TimestampUtc,
                diagnostic.RequestId,
                diagnostic.ExternalRequestId,
                diagnostic.ConfigVersion,
                diagnostic.ListenerName,
                diagnostic.Transport,
                diagnostic.ClientEndpoint,
                diagnostic.Method,
                diagnostic.Host,
                diagnostic.Target,
                diagnostic.RouteName,
                diagnostic.UpstreamName,
                diagnostic.UpstreamEndpoint,
                diagnostic.ResponseStatusCode,
                diagnostic.DurationMilliseconds,
                diagnostic.FailureKind,
                diagnostic.ResponseStarted,
                diagnostic.KeepClientConnectionOpen,
                diagnostic.IsUpgrade,
                diagnostic.TunnelEstablished,
                diagnostic.TunnelCloseReason,
                diagnostic.TunnelBytesClientToUpstream,
                diagnostic.TunnelBytesUpstreamToClient))
            .ToArray();
    }
}
