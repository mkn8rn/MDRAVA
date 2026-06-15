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
    RuntimeAcmeProjection Acme,
    RuntimeTimeoutsProjection Timeouts,
    RuntimeConnectionLimitsProjection ConnectionLimits,
    RuntimeObservabilityProjection Observability,
    RuntimeLimitsProjection Limits,
    RuntimeForwardedHeadersProjection ForwardedHeaders,
    IReadOnlyList<RuntimeCertificateProjection> Certificates,
    IReadOnlyList<RuntimeListenerProjection> Listeners,
    IReadOnlyList<RuntimeRouteProjection> Routes)
{
    private IReadOnlyList<string> _sourceFiles = ConfigurationManagementList.Copy(SourceFiles);
    private IReadOnlyList<RuntimeCertificateProjection> _certificates = ConfigurationManagementList.Copy(Certificates);
    private IReadOnlyList<RuntimeListenerProjection> _listeners = ConfigurationManagementList.Copy(Listeners);
    private IReadOnlyList<RuntimeRouteProjection> _routes = ConfigurationManagementList.Copy(Routes);

    public IReadOnlyList<string> SourceFiles
    {
        get => _sourceFiles;
        init => _sourceFiles = ConfigurationManagementList.Copy(value);
    }

    public IReadOnlyList<RuntimeCertificateProjection> Certificates
    {
        get => _certificates;
        init => _certificates = ConfigurationManagementList.Copy(value);
    }

    public IReadOnlyList<RuntimeListenerProjection> Listeners
    {
        get => _listeners;
        init => _listeners = ConfigurationManagementList.Copy(value);
    }

    public IReadOnlyList<RuntimeRouteProjection> Routes
    {
        get => _routes;
        init => _routes = ConfigurationManagementList.Copy(value);
    }

    public RuntimeMetricsProjection Metrics { get; init; } = RuntimeMetricsProjection.Default;

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
