using MDRAVA.BLL.ControlPlane;
using MDRAVA.BLL.ControlPlane.Metrics;
using MDRAVA.BLL.ControlPlane.RequestDiagnostics;
using MDRAVA.BLL.Infrastructure;
using Microsoft.Extensions.Logging;

namespace MDRAVA.INF.Observability;

public sealed class AccessLogEmitter
{
    private const int MaxDiagnosticTextLength = 512;

    private readonly RecentRequestDiagnosticsStore _diagnostics;
    private readonly ProxyMetrics _metrics;
    private readonly ILogger<AccessLogEmitter> _logger;
    private readonly IProxyLogPersistenceStore? _logPersistenceStore;

    public AccessLogEmitter(
        RecentRequestDiagnosticsStore diagnostics,
        ProxyMetrics metrics,
        ILogger<AccessLogEmitter> logger,
        IProxyLogPersistenceStore? logPersistenceStore = null)
    {
        _diagnostics = diagnostics;
        _metrics = metrics;
        _logger = logger;
        _logPersistenceStore = logPersistenceStore;
    }

    public void Complete(ProxyRequestContext context, bool accessLogEnabled, int diagnosticsCapacity)
    {
        var diagnostic = new ProxyRequestDiagnosticSourceEvent(
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

        _metrics.RequestCompleted(
            context.SiteName,
            context.RouteName,
            context.RouteAction,
            context.ResponseStatusCode);

        if (!accessLogEnabled)
        {
            return;
        }

        _metrics.AccessLogEmitted();
        _logPersistenceStore?.WriteAccess(new ProxyAccessLogEntry(
            diagnostic.TimestampUtc,
            diagnostic.RequestId,
            diagnostic.ConfigVersion,
            diagnostic.ListenerName,
            diagnostic.Transport,
            context.Protocol,
            diagnostic.Method,
            diagnostic.Host,
            diagnostic.Target,
            context.SiteName,
            diagnostic.RouteName,
            context.RouteAction,
            diagnostic.UpstreamName,
            diagnostic.UpstreamEndpoint,
            diagnostic.ResponseStatusCode,
            diagnostic.DurationMilliseconds,
            diagnostic.FailureKind,
            diagnostic.ResponseStarted,
            diagnostic.KeepClientConnectionOpen,
            diagnostic.IsUpgrade,
            diagnostic.TunnelEstablished));
        _logger.LogInformation(
            "Proxy access {RequestId} listener={ListenerName} transport={Transport} protocol={Protocol} client={ClientEndpoint} method={Method} host={Host} targetPath={TargetPath} route={RouteName} upstream={UpstreamName} upstreamEndpoint={UpstreamEndpoint} status={StatusCode} durationMs={DurationMilliseconds} failure={FailureKind} responseStarted={ResponseStarted} keepAlive={KeepAlive} upgrade={IsUpgrade} tunnel={TunnelEstablished} configVersion={ConfigVersion}",
            diagnostic.RequestId,
            diagnostic.ListenerName,
            diagnostic.Transport,
            context.Protocol,
            diagnostic.ClientEndpoint,
            diagnostic.Method,
            diagnostic.Host,
            StripQuery(diagnostic.Target),
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

    private static string? StripQuery(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var queryIndex = value.IndexOfAny(['?', '#']);
        return queryIndex >= 0 ? value[..queryIndex] : value;
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
