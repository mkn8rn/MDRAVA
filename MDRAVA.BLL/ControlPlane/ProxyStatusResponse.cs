using MDRAVA.BLL.ControlPlane.ConfigLint;
using MDRAVA.BLL.ControlPlane.RouteDiagnostics;

namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyStatusResponse(
    bool ListenerLive,
    string? ListenerName,
    string? Endpoint,
    DateTimeOffset? StartedAt,
    DateTimeOffset? StoppedAt,
    string? LastError,
    bool IsShuttingDown,
    DateTimeOffset? ShutdownStartedAtUtc,
    DateTimeOffset? ShutdownDeadlineUtc,
    int? ConfigVersion,
    DateTimeOffset? ConfigLoadedAtUtc,
    int ConfiguredListeners,
    int ConfiguredRoutes,
    ProxyMetricsSnapshot Metrics,
    IReadOnlyList<ProxyUpstreamStatusResponse> Upstreams)
{
    public IReadOnlyList<ProxyListenerStatus> Listeners { get; init; } = [];

    public ProxyListenerReloadResult? LastListenerReload { get; init; }

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

    public RouteDiagnosticsStatus RouteDiagnostics { get; init; } = RouteDiagnosticsStatus.Enabled;

    public ConfigLintStatus ConfigLint { get; init; } = ConfigLintStatus.Empty;

    public ProxyLogPersistenceStatus LogPersistence { get; init; } = ProxyLogPersistenceStatus.Unknown;

    public ProxyReadinessStatus Readiness { get; init; } = ProxyReadinessStatus.Unknown;

    public ProxySubsystemSummaries Subsystems { get; init; } = ProxySubsystemSummaries.Unknown;

    public ProxyRuntimePreflightStatus RuntimePreflight { get; init; } = ProxyRuntimePreflightStatus.Unknown;
}
