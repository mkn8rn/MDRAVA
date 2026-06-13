using BusinessProxyConfigurationProjection =
    MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationProjection;
using BusinessRuntimeAdminSecurityProjection = MDRAVA.BLL.Configuration.RuntimeAdminSecurityProjection;
using BusinessRuntimeConnectionLimits = MDRAVA.BLL.Configuration.RuntimeConnectionLimits;
using BusinessRuntimeForwardedHeadersOptions = MDRAVA.BLL.Configuration.RuntimeForwardedHeadersOptions;
using BusinessRuntimeLimits = MDRAVA.BLL.Configuration.RuntimeLimits;
using BusinessRuntimeTimeouts = MDRAVA.BLL.Configuration.RuntimeTimeouts;

namespace MDRAVA.API.Controllers;

public sealed record ProxyConfigurationResponse(
    int Version,
    DateTimeOffset LoadedAtUtc,
    string SourceDirectory,
    IReadOnlyList<string> SourceFiles,
    ProxyConfigurationDiscoveryResponse Discovery,
    RuntimeAdminSecurityResponse AdminSecurity,
    RuntimeAcmeResponse Acme,
    RuntimeTimeoutsResponse Timeouts,
    RuntimeConnectionLimitsResponse ConnectionLimits,
    RuntimeObservabilityResponse Observability,
    RuntimeLimitsResponse Limits,
    RuntimeForwardedHeadersResponse ForwardedHeaders,
    IReadOnlyList<RuntimeCertificateResponse> Certificates,
    IReadOnlyList<RuntimeListenerResponse> Listeners,
    IReadOnlyList<RuntimeRouteResponse> Routes)
{
    public RuntimeMetricsResponse Metrics { get; init; } = null!;

    public RuntimeHttp3SupportResponse Http3 { get; init; } = null!;

    public static ProxyConfigurationResponse FromProjection(BusinessProxyConfigurationProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new ProxyConfigurationResponse(
            projection.Version,
            projection.LoadedAtUtc,
            projection.SourceDirectory,
            projection.SourceFiles.ToArray(),
            ProxyConfigurationDiscoveryResponse.FromDiscovery(projection.Discovery),
            RuntimeAdminSecurityResponse.FromProjection(projection.AdminSecurity),
            RuntimeAcmeResponse.FromOptions(projection.Acme),
            RuntimeTimeoutsResponse.FromTimeouts(projection.Timeouts),
            RuntimeConnectionLimitsResponse.FromLimits(projection.ConnectionLimits),
            RuntimeObservabilityResponse.FromOptions(projection.Observability),
            RuntimeLimitsResponse.FromLimits(projection.Limits),
            RuntimeForwardedHeadersResponse.FromOptions(projection.ForwardedHeaders),
            RuntimeCertificateResponse.FromCertificates(projection.Certificates),
            RuntimeListenerResponse.FromListeners(projection.Listeners),
            RuntimeRouteResponse.FromRoutes(projection.Routes))
        {
            Metrics = RuntimeMetricsResponse.FromOptions(projection.Metrics),
            Http3 = RuntimeHttp3SupportResponse.FromProjection(projection.Http3)
        };
    }
}

public sealed record RuntimeAdminSecurityResponse(
    IReadOnlyList<string> Urls,
    bool RequireAuthentication,
    bool HasConfiguredToken,
    string? Token,
    string TokenEnvironmentVariable,
    string TokenSource,
    int RecentAuditCapacity)
{
    public static RuntimeAdminSecurityResponse FromProjection(BusinessRuntimeAdminSecurityProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeAdminSecurityResponse(
            projection.Urls.ToArray(),
            projection.RequireAuthentication,
            projection.HasConfiguredToken,
            projection.Token,
            projection.TokenEnvironmentVariable,
            projection.TokenSource,
            projection.RecentAuditCapacity);
    }
}

public sealed record RuntimeTimeoutsResponse(
    TimeSpan ClientRequestHeadTimeout,
    TimeSpan ClientRequestBodyIdleTimeout,
    TimeSpan UpstreamConnectTimeout,
    TimeSpan UpstreamResponseHeadTimeout,
    TimeSpan UpstreamResponseBodyIdleTimeout,
    TimeSpan DownstreamWriteTimeout,
    TimeSpan TlsHandshakeTimeout,
    TimeSpan ClientKeepAliveIdleTimeout,
    TimeSpan UpstreamIdleConnectionLifetime,
    TimeSpan TunnelIdleTimeout)
{
    public static RuntimeTimeoutsResponse FromTimeouts(BusinessRuntimeTimeouts timeouts)
    {
        ArgumentNullException.ThrowIfNull(timeouts);

        return new RuntimeTimeoutsResponse(
            timeouts.ClientRequestHeadTimeout,
            timeouts.ClientRequestBodyIdleTimeout,
            timeouts.UpstreamConnectTimeout,
            timeouts.UpstreamResponseHeadTimeout,
            timeouts.UpstreamResponseBodyIdleTimeout,
            timeouts.DownstreamWriteTimeout,
            timeouts.TlsHandshakeTimeout,
            timeouts.ClientKeepAliveIdleTimeout,
            timeouts.UpstreamIdleConnectionLifetime,
            timeouts.TunnelIdleTimeout);
    }
}

public sealed record RuntimeConnectionLimitsResponse(
    int MaxRequestsPerClientConnection,
    int MaxIdleUpstreamConnectionsPerUpstream,
    int MaxActiveUpgradedTunnels)
{
    public static RuntimeConnectionLimitsResponse FromLimits(BusinessRuntimeConnectionLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        return new RuntimeConnectionLimitsResponse(
            limits.MaxRequestsPerClientConnection,
            limits.MaxIdleUpstreamConnectionsPerUpstream,
            limits.MaxActiveUpgradedTunnels);
    }
}

public sealed record RuntimeLimitsResponse(
    int MaxActiveClientConnections,
    int MaxConcurrentTlsHandshakes,
    int RequestsPerMinutePerIp,
    int UpgradeRequestsPerMinutePerIp,
    int MaxRequestHeadBytes,
    int MaxHeaderCount,
    int MaxHeaderLineBytes,
    long MaxRequestBodyBytes,
    int MaxPathBytes,
    TimeSpan ShutdownGracePeriod)
{
    public static RuntimeLimitsResponse FromLimits(BusinessRuntimeLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        return new RuntimeLimitsResponse(
            limits.MaxActiveClientConnections,
            limits.MaxConcurrentTlsHandshakes,
            limits.RequestsPerMinutePerIp,
            limits.UpgradeRequestsPerMinutePerIp,
            limits.MaxRequestHeadBytes,
            limits.MaxHeaderCount,
            limits.MaxHeaderLineBytes,
            limits.MaxRequestBodyBytes,
            limits.MaxPathBytes,
            limits.ShutdownGracePeriod);
    }
}

public sealed record RuntimeForwardedHeadersResponse(
    bool Enabled,
    IReadOnlyList<string> TrustedProxies)
{
    public static RuntimeForwardedHeadersResponse FromOptions(BusinessRuntimeForwardedHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RuntimeForwardedHeadersResponse(options.Enabled, options.TrustedProxies.ToArray());
    }
}
