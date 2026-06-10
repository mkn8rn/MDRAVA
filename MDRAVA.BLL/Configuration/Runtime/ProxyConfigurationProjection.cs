using MDRAVA.BLL.ControlPlane.Http3;

namespace MDRAVA.BLL.Configuration;

public sealed record ProxyConfigurationProjection(
    int Version,
    DateTimeOffset LoadedAtUtc,
    string SourceDirectory,
    IReadOnlyList<string> SourceFiles,
    ProxyConfigurationDiscovery Discovery,
    RuntimeAdminSecurityProjection AdminSecurity,
    RuntimeAcmeProjection Acme,
    RuntimeTimeouts Timeouts,
    RuntimeConnectionLimits ConnectionLimits,
    RuntimeObservabilityOptions Observability,
    RuntimeLimits Limits,
    RuntimeForwardedHeadersOptions ForwardedHeaders,
    IReadOnlyList<RuntimeCertificateProjection> Certificates,
    IReadOnlyList<RuntimeListener> Listeners,
    IReadOnlyList<RuntimeRoute> Routes)
{
    public RuntimeMetricsOptions Metrics { get; init; } = RuntimeMetricsOptions.Default;

    public RuntimeHttp3SupportProjection Http3 { get; init; } = new(
        "unknown",
        QuicListenerSupported: false,
        QuicConnectionSupported: false,
        "disabled",
        "disabled",
        EnabledForTraffic: false,
        QuicListenerReady: false,
        AltSvcConfigured: false,
        AltSvcActive: false,
        AltSvcMaxAgeSeconds: null,
        "not_configured",
        UdpQuicListenerIdentityModeled: true,
        "client_http3_default_enabled_for_eligible_tls_proxy_listeners");
}
