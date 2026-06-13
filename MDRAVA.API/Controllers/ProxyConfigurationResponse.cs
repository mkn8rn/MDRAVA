using BusinessProxyConfigurationProjection =
    MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationProjection;
using BusinessRuntimeAcmeCertificateOptions = MDRAVA.BLL.Configuration.RuntimeAcmeCertificateOptions;
using BusinessRuntimeAcmeOptions = MDRAVA.BLL.Configuration.RuntimeAcmeOptions;
using BusinessRuntimeAdminSecurityProjection = MDRAVA.BLL.Configuration.RuntimeAdminSecurityProjection;
using BusinessRuntimeCertificateProjection = MDRAVA.BLL.Configuration.RuntimeCertificateProjection;
using BusinessRuntimeConnectionLimits = MDRAVA.BLL.Configuration.RuntimeConnectionLimits;
using BusinessRuntimeForwardedHeadersOptions = MDRAVA.BLL.Configuration.RuntimeForwardedHeadersOptions;
using BusinessRuntimeLimits = MDRAVA.BLL.Configuration.RuntimeLimits;
using BusinessRuntimeLogPersistenceOptions = MDRAVA.BLL.Configuration.RuntimeLogPersistenceOptions;
using BusinessRuntimeMetricsOptions = MDRAVA.BLL.Configuration.RuntimeMetricsOptions;
using BusinessRuntimeObservabilityOptions = MDRAVA.BLL.Configuration.RuntimeObservabilityOptions;
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

public sealed record RuntimeAcmeResponse(
    bool Enabled,
    bool UseStaging,
    string DirectoryUrl,
    IReadOnlyList<string> ContactEmails,
    bool TermsAccepted,
    string StoragePath,
    int RenewBeforeDays,
    int CheckIntervalMinutes,
    int RetryAfterMinutes,
    IReadOnlyList<RuntimeAcmeCertificateResponse> Certificates)
{
    public static RuntimeAcmeResponse FromOptions(BusinessRuntimeAcmeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RuntimeAcmeResponse(
            options.Enabled,
            options.UseStaging,
            options.DirectoryUrl,
            options.ContactEmails.ToArray(),
            options.TermsAccepted,
            options.StoragePath,
            options.RenewBeforeDays,
            options.CheckIntervalMinutes,
            options.RetryAfterMinutes,
            options.Certificates.Select(RuntimeAcmeCertificateResponse.FromOptions).ToArray());
    }
}

public sealed record RuntimeAcmeCertificateResponse(
    string Id,
    bool Enabled,
    IReadOnlyList<string> Domains,
    int RenewBeforeDays)
{
    public static RuntimeAcmeCertificateResponse FromOptions(BusinessRuntimeAcmeCertificateOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RuntimeAcmeCertificateResponse(
            options.Id,
            options.Enabled,
            options.Domains.ToArray(),
            options.RenewBeforeDays);
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

public sealed record RuntimeObservabilityResponse(
    bool AccessLogEnabled,
    int RecentDiagnosticsCapacity,
    RuntimeLogPersistenceResponse LogPersistence)
{
    public static RuntimeObservabilityResponse FromOptions(BusinessRuntimeObservabilityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RuntimeObservabilityResponse(
            options.AccessLogEnabled,
            options.RecentDiagnosticsCapacity,
            RuntimeLogPersistenceResponse.FromOptions(options.LogPersistence));
    }
}

public sealed record RuntimeLogPersistenceResponse(
    bool AccessLogEnabled,
    bool AdminAuditEnabled,
    long MaxFileBytes,
    int MaxFiles)
{
    public static RuntimeLogPersistenceResponse FromOptions(BusinessRuntimeLogPersistenceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RuntimeLogPersistenceResponse(
            options.AccessLogEnabled,
            options.AdminAuditEnabled,
            options.MaxFileBytes,
            options.MaxFiles);
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

public sealed record RuntimeCertificateResponse(
    string Id,
    string Path,
    string Format,
    string Source,
    IReadOnlyList<string> Domains,
    bool HasConfiguredPassword,
    string? Subject,
    string? Thumbprint,
    DateTime NotBefore,
    DateTime NotAfter)
{
    public static IReadOnlyList<RuntimeCertificateResponse> FromCertificates(
        IReadOnlyList<BusinessRuntimeCertificateProjection> certificates)
    {
        ArgumentNullException.ThrowIfNull(certificates);

        return certificates.Select(FromCertificate).ToArray();
    }

    private static RuntimeCertificateResponse FromCertificate(BusinessRuntimeCertificateProjection certificate)
    {
        ArgumentNullException.ThrowIfNull(certificate);

        return new RuntimeCertificateResponse(
            certificate.Id,
            certificate.Path,
            certificate.Format,
            certificate.Source,
            certificate.Domains.ToArray(),
            certificate.HasConfiguredPassword,
            certificate.Subject,
            certificate.Thumbprint,
            certificate.NotBefore,
            certificate.NotAfter);
    }
}

public sealed record RuntimeMetricsResponse(
    bool Enabled,
    string EndpointPath,
    bool ProtectedByAdminAuth,
    bool IncludePerRouteLabels,
    bool IncludePerUpstreamLabels,
    bool PublicMetricsEnabled)
{
    public static RuntimeMetricsResponse FromOptions(BusinessRuntimeMetricsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RuntimeMetricsResponse(
            options.Enabled,
            options.EndpointPath,
            options.ProtectedByAdminAuth,
            options.IncludePerRouteLabels,
            options.IncludePerUpstreamLabels,
            options.PublicMetricsEnabled);
    }
}
