namespace MDRAVA.API.Proxy.Observability;

public sealed class AccessLogEmitter
{
    private const int MaxDiagnosticTextLength = 512;

    private readonly RecentRequestDiagnosticsStore _diagnostics;
    private readonly Metrics.ProxyMetrics _metrics;
    private readonly ILogger<AccessLogEmitter> _logger;

    public AccessLogEmitter(
        RecentRequestDiagnosticsStore diagnostics,
        Metrics.ProxyMetrics metrics,
        ILogger<AccessLogEmitter> logger)
    {
        _diagnostics = diagnostics;
        _metrics = metrics;
        _logger = logger;
    }

    public void Complete(ProxyRequestContext context, bool accessLogEnabled, int diagnosticsCapacity)
    {
        var diagnostic = new ProxyRequestDiagnosticEvent(
            context.StartedAtUtc,
            TruncateRequired(context.RequestId),
            Truncate(context.ExternalRequestId),
            context.ConfigVersion,
            TruncateRequired(context.ListenerName),
            TruncateRequired(context.Transport),
            Truncate(context.ClientEndpoint),
            Truncate(context.Method),
            Truncate(context.Host),
            Truncate(context.Target),
            Truncate(context.RouteName),
            Truncate(context.UpstreamName),
            Truncate(context.UpstreamEndpoint),
            context.ResponseStatusCode,
            (long)context.Elapsed.TotalMilliseconds,
            context.FailureKind.ToString(),
            context.ResponseStarted,
            context.KeepClientConnectionOpen,
            context.IsUpgrade,
            context.TunnelEstablished,
            Truncate(context.TunnelCloseReason),
            context.TunnelBytesClientToUpstream,
            context.TunnelBytesUpstreamToClient);

        _diagnostics.Add(diagnostic, diagnosticsCapacity);

        if (context.FailureKind != ProxyFailureKind.None)
        {
            _metrics.RequestFailed(context.FailureKind);
        }

        if (!accessLogEnabled)
        {
            return;
        }

        _metrics.AccessLogEmitted();
        _logger.LogInformation(
            "Proxy access {RequestId} externalRequestId={ExternalRequestId} listener={ListenerName} transport={Transport} client={ClientEndpoint} method={Method} host={Host} target={Target} route={RouteName} upstream={UpstreamName} upstreamEndpoint={UpstreamEndpoint} status={StatusCode} durationMs={DurationMilliseconds} failure={FailureKind} responseStarted={ResponseStarted} keepAlive={KeepAlive} upgrade={IsUpgrade} tunnel={TunnelEstablished} configVersion={ConfigVersion}",
            diagnostic.RequestId,
            diagnostic.ExternalRequestId,
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
            diagnostic.ConfigVersion);
    }

    private static string? Truncate(string? value)
    {
        if (value is null || value.Length <= MaxDiagnosticTextLength)
        {
            return value;
        }

        return value[..MaxDiagnosticTextLength];
    }

    private static string TruncateRequired(string value)
    {
        return value.Length <= MaxDiagnosticTextLength
            ? value
            : value[..MaxDiagnosticTextLength];
    }
}
