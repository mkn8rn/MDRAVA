using BusinessProxyConfigurationProjection =
    MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationProjection;
using BusinessRuntimeAcmeCertificateOptions = MDRAVA.BLL.Configuration.RuntimeAcmeCertificateOptions;
using BusinessRuntimeAcmeOptions = MDRAVA.BLL.Configuration.RuntimeAcmeOptions;
using BusinessRuntimeAdminSecurityProjection = MDRAVA.BLL.Configuration.RuntimeAdminSecurityProjection;
using BusinessRuntimeCertificateProjection = MDRAVA.BLL.Configuration.RuntimeCertificateProjection;
using BusinessRuntimeConnectionLimits = MDRAVA.BLL.Configuration.RuntimeConnectionLimits;
using BusinessRuntimeForwardedHeadersOptions = MDRAVA.BLL.Configuration.RuntimeForwardedHeadersOptions;
using BusinessRuntimeHttp2Limits = MDRAVA.BLL.Configuration.RuntimeHttp2Limits;
using BusinessRuntimeHttp3AltSvcOptions = MDRAVA.BLL.Configuration.RuntimeHttp3AltSvcOptions;
using BusinessRuntimeHttp3Enablement = MDRAVA.BLL.Configuration.RuntimeHttp3Enablement;
using BusinessRuntimeHttp3ListenerReadiness = MDRAVA.BLL.Configuration.RuntimeHttp3ListenerReadiness;
using BusinessRuntimeLimits = MDRAVA.BLL.Configuration.RuntimeLimits;
using BusinessRuntimeListener = MDRAVA.BLL.Configuration.RuntimeListener;
using BusinessRuntimeListenerIdentity = MDRAVA.BLL.Configuration.RuntimeListenerIdentity;
using BusinessRuntimeListenerProtocols = MDRAVA.BLL.Configuration.RuntimeListenerProtocols;
using BusinessRuntimeListenerTransport = MDRAVA.BLL.Configuration.RuntimeListenerTransport;
using BusinessRuntimeLogPersistenceOptions = MDRAVA.BLL.Configuration.RuntimeLogPersistenceOptions;
using BusinessRuntimeMetricsOptions = MDRAVA.BLL.Configuration.RuntimeMetricsOptions;
using BusinessRuntimeObservabilityOptions = MDRAVA.BLL.Configuration.RuntimeObservabilityOptions;
using BusinessRuntimeQuicListenerIdentity = MDRAVA.BLL.Configuration.RuntimeQuicListenerIdentity;
using BusinessRuntimeSniCertificateBinding = MDRAVA.BLL.Configuration.RuntimeSniCertificateBinding;
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

public sealed record RuntimeListenerResponse(
    string Name,
    string Address,
    int Port,
    bool Enabled,
    RuntimeListenerTransportResponse Transport,
    string? DefaultCertificateId,
    IReadOnlyList<RuntimeSniCertificateBindingResponse> SniCertificates,
    int Backlog,
    int MaxRequestHeadBytes,
    int MaxResponseHeadBytes,
    int MaxChunkLineBytes,
    int ForwardingBufferBytes)
{
    public RuntimeListenerIdentityResponse Identity { get; init; } = null!;

    public RuntimeListenerProtocolsResponse Protocols { get; init; }

    public RuntimeHttp3EnablementResponse Http3Enablement { get; init; }

    public RuntimeHttp3AltSvcResponse Http3AltSvc { get; init; } = null!;

    public RuntimeHttp2LimitsResponse Http2Limits { get; init; } = null!;

    public bool TcpTrafficEnabled { get; init; }

    public bool Http3ProtocolConfigured { get; init; }

    public RuntimeQuicListenerIdentityResponse? QuicIdentity { get; init; }

    public RuntimeHttp3ListenerReadinessResponse Http3 { get; init; } = null!;

    public static IReadOnlyList<RuntimeListenerResponse> FromListeners(
        IReadOnlyList<BusinessRuntimeListener> listeners)
    {
        ArgumentNullException.ThrowIfNull(listeners);

        return listeners.Select(FromListener).ToArray();
    }

    private static RuntimeListenerResponse FromListener(BusinessRuntimeListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        return new RuntimeListenerResponse(
            listener.Name,
            listener.Address,
            listener.Port,
            listener.Enabled,
            RuntimeListenerTransportResponseMapper.FromTransport(listener.Transport),
            listener.DefaultCertificateId,
            RuntimeSniCertificateBindingResponse.FromBindings(listener.SniCertificates),
            listener.Backlog,
            listener.MaxRequestHeadBytes,
            listener.MaxResponseHeadBytes,
            listener.MaxChunkLineBytes,
            listener.ForwardingBufferBytes)
        {
            Identity = RuntimeListenerIdentityResponse.FromIdentity(listener.Identity),
            Protocols = RuntimeListenerProtocolsResponseMapper.FromProtocols(listener.Protocols),
            Http3Enablement = RuntimeHttp3EnablementResponseMapper.FromEnablement(listener.Http3Enablement),
            Http3AltSvc = RuntimeHttp3AltSvcResponse.FromOptions(listener.Http3AltSvc),
            Http2Limits = RuntimeHttp2LimitsResponse.FromLimits(listener.Http2Limits),
            TcpTrafficEnabled = listener.TcpTrafficEnabled,
            Http3ProtocolConfigured = listener.Http3ProtocolConfigured,
            QuicIdentity = listener.QuicIdentity is null
                ? null
                : RuntimeQuicListenerIdentityResponse.FromIdentity(listener.QuicIdentity),
            Http3 = RuntimeHttp3ListenerReadinessResponse.FromReadiness(listener.Http3)
        };
    }
}

public sealed record RuntimeListenerIdentityResponse(
    string Name,
    string Address,
    int Port,
    RuntimeListenerTransportResponse Transport,
    bool TlsEnabled)
{
    public string Key { get; init; } = "";

    public string BindKey { get; init; } = "";

    public static RuntimeListenerIdentityResponse FromIdentity(BusinessRuntimeListenerIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return new RuntimeListenerIdentityResponse(
            identity.Name,
            identity.Address,
            identity.Port,
            RuntimeListenerTransportResponseMapper.FromTransport(identity.Transport),
            identity.TlsEnabled)
        {
            Key = identity.Key,
            BindKey = identity.BindKey
        };
    }
}

public sealed record RuntimeSniCertificateBindingResponse(
    string HostName,
    string CertificateId)
{
    public static IReadOnlyList<RuntimeSniCertificateBindingResponse> FromBindings(
        IReadOnlyList<BusinessRuntimeSniCertificateBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);

        return bindings.Select(FromBinding).ToArray();
    }

    private static RuntimeSniCertificateBindingResponse FromBinding(BusinessRuntimeSniCertificateBinding binding)
    {
        ArgumentNullException.ThrowIfNull(binding);

        return new RuntimeSniCertificateBindingResponse(binding.HostName, binding.CertificateId);
    }
}

public sealed record RuntimeHttp3AltSvcResponse(
    bool Enabled,
    int MaxAgeSeconds)
{
    public static RuntimeHttp3AltSvcResponse FromOptions(BusinessRuntimeHttp3AltSvcOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RuntimeHttp3AltSvcResponse(options.Enabled, options.MaxAgeSeconds);
    }
}

public sealed record RuntimeHttp2LimitsResponse(
    int MaxConcurrentStreams,
    int MaxHeaderListBytes,
    int MaxFrameSize)
{
    public static RuntimeHttp2LimitsResponse FromLimits(BusinessRuntimeHttp2Limits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);

        return new RuntimeHttp2LimitsResponse(
            limits.MaxConcurrentStreams,
            limits.MaxHeaderListBytes,
            limits.MaxFrameSize);
    }
}

public sealed record RuntimeHttp3ListenerReadinessResponse(
    bool Configured,
    bool DefaultEnabled,
    string EnablementLevel,
    bool EnabledForTraffic,
    string DisabledReason,
    bool AltSvcConfigured,
    int AltSvcMaxAgeSeconds,
    bool UdpQuicListenerIdentityModeled,
    RuntimeQuicListenerIdentityResponse? QuicIdentity)
{
    public static RuntimeHttp3ListenerReadinessResponse FromReadiness(
        BusinessRuntimeHttp3ListenerReadiness readiness)
    {
        ArgumentNullException.ThrowIfNull(readiness);

        return new RuntimeHttp3ListenerReadinessResponse(
            readiness.Configured,
            readiness.DefaultEnabled,
            readiness.EnablementLevel,
            readiness.EnabledForTraffic,
            readiness.DisabledReason,
            readiness.AltSvcConfigured,
            readiness.AltSvcMaxAgeSeconds,
            readiness.UdpQuicListenerIdentityModeled,
            readiness.QuicIdentity is null
                ? null
                : RuntimeQuicListenerIdentityResponse.FromIdentity(readiness.QuicIdentity));
    }
}

public sealed record RuntimeQuicListenerIdentityResponse(
    string Name,
    string Address,
    int Port,
    bool TlsEnabled)
{
    public string Key { get; init; } = "";

    public string BindKey { get; init; } = "";

    public static RuntimeQuicListenerIdentityResponse FromIdentity(BusinessRuntimeQuicListenerIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return new RuntimeQuicListenerIdentityResponse(
            identity.Name,
            identity.Address,
            identity.Port,
            identity.TlsEnabled)
        {
            Key = identity.Key,
            BindKey = identity.BindKey
        };
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

public enum RuntimeListenerTransportResponse
{
    Http = 0,
    Https = 1
}

public static class RuntimeListenerTransportResponseMapper
{
    public static RuntimeListenerTransportResponse FromTransport(BusinessRuntimeListenerTransport transport)
    {
        return transport switch
        {
            BusinessRuntimeListenerTransport.Http => RuntimeListenerTransportResponse.Http,
            BusinessRuntimeListenerTransport.Https => RuntimeListenerTransportResponse.Https,
            _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null)
        };
    }
}

[Flags]
public enum RuntimeListenerProtocolsResponse
{
    None = 0,
    Http1 = 1,
    Http2 = 2,
    Http3 = 4,
    Http1AndHttp2 = Http1 | Http2,
    Http1AndHttp3 = Http1 | Http3,
    Http2AndHttp3 = Http2 | Http3,
    Http1AndHttp2AndHttp3 = Http1 | Http2 | Http3
}

public static class RuntimeListenerProtocolsResponseMapper
{
    public static RuntimeListenerProtocolsResponse FromProtocols(BusinessRuntimeListenerProtocols protocols)
    {
        return protocols switch
        {
            BusinessRuntimeListenerProtocols.None => RuntimeListenerProtocolsResponse.None,
            BusinessRuntimeListenerProtocols.Http1 => RuntimeListenerProtocolsResponse.Http1,
            BusinessRuntimeListenerProtocols.Http2 => RuntimeListenerProtocolsResponse.Http2,
            BusinessRuntimeListenerProtocols.Http3 => RuntimeListenerProtocolsResponse.Http3,
            BusinessRuntimeListenerProtocols.Http1AndHttp2 => RuntimeListenerProtocolsResponse.Http1AndHttp2,
            BusinessRuntimeListenerProtocols.Http1AndHttp3 => RuntimeListenerProtocolsResponse.Http1AndHttp3,
            BusinessRuntimeListenerProtocols.Http2AndHttp3 => RuntimeListenerProtocolsResponse.Http2AndHttp3,
            BusinessRuntimeListenerProtocols.Http1AndHttp2AndHttp3 =>
                RuntimeListenerProtocolsResponse.Http1AndHttp2AndHttp3,
            _ => throw new ArgumentOutOfRangeException(nameof(protocols), protocols, null)
        };
    }
}

public enum RuntimeHttp3EnablementResponse
{
    Default = 0,
    Disabled = 1
}

public static class RuntimeHttp3EnablementResponseMapper
{
    public static RuntimeHttp3EnablementResponse FromEnablement(BusinessRuntimeHttp3Enablement enablement)
    {
        return enablement switch
        {
            BusinessRuntimeHttp3Enablement.Default => RuntimeHttp3EnablementResponse.Default,
            BusinessRuntimeHttp3Enablement.Disabled => RuntimeHttp3EnablementResponse.Disabled,
            _ => throw new ArgumentOutOfRangeException(nameof(enablement), enablement, null)
        };
    }
}
