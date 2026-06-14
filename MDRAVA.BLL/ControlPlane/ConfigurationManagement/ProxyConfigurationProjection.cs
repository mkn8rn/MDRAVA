using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public sealed record ProxyConfigurationProjection(
    int Version,
    DateTimeOffset LoadedAtUtc,
    string SourceDirectory,
    IReadOnlyList<string> SourceFiles,
    ProxyConfigurationDiscovery Discovery,
    RuntimeAdminSecurityProjection AdminSecurity,
    RuntimeAcmeOptions Acme,
    RuntimeTimeoutsProjection Timeouts,
    RuntimeConnectionLimitsProjection ConnectionLimits,
    RuntimeObservabilityOptions Observability,
    RuntimeLimitsProjection Limits,
    RuntimeForwardedHeadersOptions ForwardedHeaders,
    IReadOnlyList<RuntimeCertificateProjection> Certificates,
    IReadOnlyList<RuntimeListenerProjection> Listeners,
    IReadOnlyList<RuntimeRouteProjection> Routes)
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
