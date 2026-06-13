using BusinessProxyAcmeSubsystemSummary = MDRAVA.BLL.ControlPlane.Status.ProxyAcmeSubsystemSummary;
using BusinessProxyCacheSubsystemSummary = MDRAVA.BLL.ControlPlane.Status.ProxyCacheSubsystemSummary;
using BusinessProxyCertificateSubsystemSummary = MDRAVA.BLL.ControlPlane.Status.ProxyCertificateSubsystemSummary;
using BusinessProxyCircuitSubsystemSummary = MDRAVA.BLL.ControlPlane.Status.ProxyCircuitSubsystemSummary;
using BusinessProxyConfigSubsystemSummary = MDRAVA.BLL.ControlPlane.Status.ProxyConfigSubsystemSummary;
using BusinessProxyLimitSubsystemSummary = MDRAVA.BLL.ControlPlane.Status.ProxyLimitSubsystemSummary;
using BusinessProxyListenerSubsystemSummary = MDRAVA.BLL.ControlPlane.Status.ProxyListenerSubsystemSummary;
using BusinessProxyLogSubsystemSummary = MDRAVA.BLL.ControlPlane.Status.ProxyLogSubsystemSummary;
using BusinessProxyProtocolSubsystemSummary = MDRAVA.BLL.ControlPlane.Status.ProxyProtocolSubsystemSummary;
using BusinessProxyRouteSubsystemSummary = MDRAVA.BLL.ControlPlane.Status.ProxyRouteSubsystemSummary;
using BusinessProxyShutdownSubsystemSummary = MDRAVA.BLL.ControlPlane.Status.ProxyShutdownSubsystemSummary;
using BusinessProxySubsystemIssueSummary = MDRAVA.BLL.ControlPlane.Status.ProxySubsystemIssueSummary;
using BusinessProxySubsystemSummaries = MDRAVA.BLL.ControlPlane.Status.ProxySubsystemSummaries;
using BusinessProxyUpstreamSubsystemSummary = MDRAVA.BLL.ControlPlane.Status.ProxyUpstreamSubsystemSummary;

namespace MDRAVA.API.Controllers;

public sealed record ProxySubsystemSummariesResponse(
    ProxyConfigSubsystemSummaryResponse Config,
    ProxyListenerSubsystemSummaryResponse Listeners,
    ProxyRouteSubsystemSummaryResponse Routes,
    ProxyCertificateSubsystemSummaryResponse Certificates,
    ProxyAcmeSubsystemSummaryResponse Acme,
    ProxyUpstreamSubsystemSummaryResponse Upstreams,
    ProxyCacheSubsystemSummaryResponse Cache,
    ProxyCircuitSubsystemSummaryResponse Circuits,
    ProxyLimitSubsystemSummaryResponse Limits,
    ProxyLogSubsystemSummaryResponse Logs,
    ProxyShutdownSubsystemSummaryResponse Shutdown,
    ProxyProtocolSubsystemSummaryResponse Protocols)
{
    public static ProxySubsystemSummariesResponse FromSummaries(BusinessProxySubsystemSummaries summaries)
    {
        ArgumentNullException.ThrowIfNull(summaries);

        return new ProxySubsystemSummariesResponse(
            ProxyConfigSubsystemSummaryResponse.FromSummary(summaries.Config),
            ProxyListenerSubsystemSummaryResponse.FromSummary(summaries.Listeners),
            ProxyRouteSubsystemSummaryResponse.FromSummary(summaries.Routes),
            ProxyCertificateSubsystemSummaryResponse.FromSummary(summaries.Certificates),
            ProxyAcmeSubsystemSummaryResponse.FromSummary(summaries.Acme),
            ProxyUpstreamSubsystemSummaryResponse.FromSummary(summaries.Upstreams),
            ProxyCacheSubsystemSummaryResponse.FromSummary(summaries.Cache),
            ProxyCircuitSubsystemSummaryResponse.FromSummary(summaries.Circuits),
            ProxyLimitSubsystemSummaryResponse.FromSummary(summaries.Limits),
            ProxyLogSubsystemSummaryResponse.FromSummary(summaries.Logs),
            ProxyShutdownSubsystemSummaryResponse.FromSummary(summaries.Shutdown),
            ProxyProtocolSubsystemSummaryResponse.FromSummary(summaries.Protocols));
    }
}

public sealed record ProxyConfigSubsystemSummaryResponse(
    bool Active,
    int? Generation,
    DateTimeOffset? LoadedAtUtc,
    bool? LastListenerReloadSucceeded,
    string? LastListenerReloadReason)
{
    public static ProxyConfigSubsystemSummaryResponse FromSummary(BusinessProxyConfigSubsystemSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ProxyConfigSubsystemSummaryResponse(
            summary.Active,
            summary.Generation,
            summary.LoadedAtUtc,
            summary.LastListenerReloadSucceeded,
            summary.LastListenerReloadReason);
    }
}

public sealed record ProxyListenerSubsystemSummaryResponse(
    int Configured,
    int Enabled,
    int Active,
    int Failed,
    int Draining,
    int Http1Enabled,
    int Http2Enabled,
    int Http3Enabled,
    int QuicReady)
{
    public static ProxyListenerSubsystemSummaryResponse FromSummary(BusinessProxyListenerSubsystemSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ProxyListenerSubsystemSummaryResponse(
            summary.Configured,
            summary.Enabled,
            summary.Active,
            summary.Failed,
            summary.Draining,
            summary.Http1Enabled,
            summary.Http2Enabled,
            summary.Http3Enabled,
            summary.QuicReady);
    }
}

public sealed record ProxyRouteSubsystemSummaryResponse(
    int Sites,
    int Routes,
    int ProxyRoutes,
    int GeneratedRoutes,
    int CacheEnabledRoutes)
{
    public static ProxyRouteSubsystemSummaryResponse FromSummary(BusinessProxyRouteSubsystemSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ProxyRouteSubsystemSummaryResponse(
            summary.Sites,
            summary.Routes,
            summary.ProxyRoutes,
            summary.GeneratedRoutes,
            summary.CacheEnabledRoutes);
    }
}

public sealed record ProxySubsystemIssueSummaryResponse(
    DateTimeOffset TimestampUtc,
    string Category,
    string Reason,
    string? AffectedIdentity)
{
    public static ProxySubsystemIssueSummaryResponse FromSummary(BusinessProxySubsystemIssueSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ProxySubsystemIssueSummaryResponse(
            summary.TimestampUtc,
            summary.Category,
            summary.Reason,
            summary.AffectedIdentity);
    }
}

public sealed record ProxyCertificateSubsystemSummaryResponse(
    int Configured,
    int Loaded,
    int MissingReferences,
    int Expired,
    int NotYetValid,
    int ExpiringSoon,
    ProxySubsystemIssueSummaryResponse? LastIssue)
{
    public static ProxyCertificateSubsystemSummaryResponse FromSummary(BusinessProxyCertificateSubsystemSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ProxyCertificateSubsystemSummaryResponse(
            summary.Configured,
            summary.Loaded,
            summary.MissingReferences,
            summary.Expired,
            summary.NotYetValid,
            summary.ExpiringSoon,
            summary.LastIssue is null ? null : ProxySubsystemIssueSummaryResponse.FromSummary(summary.LastIssue));
    }
}

public sealed record ProxyAcmeSubsystemSummaryResponse(
    bool Enabled,
    int Configured,
    int Active,
    int Failed,
    int RenewalBackoff,
    ProxySubsystemIssueSummaryResponse? LastIssue)
{
    public static ProxyAcmeSubsystemSummaryResponse FromSummary(BusinessProxyAcmeSubsystemSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ProxyAcmeSubsystemSummaryResponse(
            summary.Enabled,
            summary.Configured,
            summary.Active,
            summary.Failed,
            summary.RenewalBackoff,
            summary.LastIssue is null ? null : ProxySubsystemIssueSummaryResponse.FromSummary(summary.LastIssue));
    }
}

public sealed record ProxyUpstreamSubsystemSummaryResponse(
    int Total,
    int Healthy,
    int Unhealthy,
    int UnknownHealth,
    int HealthChecksEnabled)
{
    public static ProxyUpstreamSubsystemSummaryResponse FromSummary(BusinessProxyUpstreamSubsystemSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ProxyUpstreamSubsystemSummaryResponse(
            summary.Total,
            summary.Healthy,
            summary.Unhealthy,
            summary.UnknownHealth,
            summary.HealthChecksEnabled);
    }
}

public sealed record ProxyCacheSubsystemSummaryResponse(
    bool Enabled,
    int EnabledRoutes,
    int EntryCount,
    long ApproximateBytes)
{
    public static ProxyCacheSubsystemSummaryResponse FromSummary(BusinessProxyCacheSubsystemSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ProxyCacheSubsystemSummaryResponse(
            summary.Enabled,
            summary.EnabledRoutes,
            summary.EntryCount,
            summary.ApproximateBytes);
    }
}

public sealed record ProxyCircuitSubsystemSummaryResponse(
    int Enabled,
    int Open,
    int HalfOpen,
    int Closed)
{
    public static ProxyCircuitSubsystemSummaryResponse FromSummary(BusinessProxyCircuitSubsystemSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ProxyCircuitSubsystemSummaryResponse(
            summary.Enabled,
            summary.Open,
            summary.HalfOpen,
            summary.Closed);
    }
}

public sealed record ProxyLimitSubsystemSummaryResponse(
    int MaxActiveClientConnections,
    long ActiveConnections,
    int MaxConcurrentTlsHandshakes,
    long ActiveTlsHandshakes,
    long ActiveHttp2Streams,
    long ActiveHttp3Streams,
    long ActiveUpstreamHttp3Streams,
    int RequestsPerMinutePerIp)
{
    public static ProxyLimitSubsystemSummaryResponse FromSummary(BusinessProxyLimitSubsystemSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ProxyLimitSubsystemSummaryResponse(
            summary.MaxActiveClientConnections,
            summary.ActiveConnections,
            summary.MaxConcurrentTlsHandshakes,
            summary.ActiveTlsHandshakes,
            summary.ActiveHttp2Streams,
            summary.ActiveHttp3Streams,
            summary.ActiveUpstreamHttp3Streams,
            summary.RequestsPerMinutePerIp);
    }
}

public sealed record ProxyLogSubsystemSummaryResponse(
    bool AccessLogPersistenceEnabled,
    bool AdminAuditPersistenceEnabled,
    string State,
    string Reason)
{
    public static ProxyLogSubsystemSummaryResponse FromSummary(BusinessProxyLogSubsystemSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ProxyLogSubsystemSummaryResponse(
            summary.AccessLogPersistenceEnabled,
            summary.AdminAuditPersistenceEnabled,
            summary.State,
            summary.Reason);
    }
}

public sealed record ProxyShutdownSubsystemSummaryResponse(
    bool IsRunning,
    bool IsShuttingDown,
    DateTimeOffset? ShutdownStartedAtUtc,
    DateTimeOffset? ShutdownDeadlineUtc)
{
    public static ProxyShutdownSubsystemSummaryResponse FromSummary(BusinessProxyShutdownSubsystemSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ProxyShutdownSubsystemSummaryResponse(
            summary.IsRunning,
            summary.IsShuttingDown,
            summary.ShutdownStartedAtUtc,
            summary.ShutdownDeadlineUtc);
    }
}

public sealed record ProxyProtocolSubsystemSummaryResponse(
    bool ClientHttp1Enabled,
    bool ClientHttp2Enabled,
    bool ClientHttp3Enabled,
    bool ClientHttp3Ready,
    bool UpstreamHttp3Configured,
    IReadOnlyList<string> UnsupportedHttp3Features)
{
    public static ProxyProtocolSubsystemSummaryResponse FromSummary(BusinessProxyProtocolSubsystemSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);

        return new ProxyProtocolSubsystemSummaryResponse(
            summary.ClientHttp1Enabled,
            summary.ClientHttp2Enabled,
            summary.ClientHttp3Enabled,
            summary.ClientHttp3Ready,
            summary.UpstreamHttp3Configured,
            summary.UnsupportedHttp3Features);
    }
}
