using MDRAVA.BLL.ControlPlane.ConfigLint;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Listeners;
using MDRAVA.BLL.ControlPlane.RouteDiagnostics;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.BLL.ControlPlane.Status;

public sealed record ProxyStatus
{
    private IReadOnlyList<ProxyListenerStatus> _listeners = [];

    public ProxyStatus(
        bool listenerLive,
        string? listenerName,
        string? endpoint,
        DateTimeOffset? startedAt,
        DateTimeOffset? stoppedAt,
        string? lastError,
        bool isShuttingDown,
        DateTimeOffset? shutdownStartedAtUtc,
        DateTimeOffset? shutdownDeadlineUtc,
        int? configVersion,
        DateTimeOffset? configLoadedAtUtc,
        int configuredListeners,
        int configuredRoutes,
        ProxyMetricsSnapshot metrics,
        IReadOnlyList<ProxyUpstreamStatus> upstreams)
    {
        ArgumentNullException.ThrowIfNull(upstreams);

        ListenerLive = listenerLive;
        ListenerName = listenerName;
        Endpoint = endpoint;
        StartedAt = startedAt;
        StoppedAt = stoppedAt;
        LastError = lastError;
        IsShuttingDown = isShuttingDown;
        ShutdownStartedAtUtc = shutdownStartedAtUtc;
        ShutdownDeadlineUtc = shutdownDeadlineUtc;
        ConfigVersion = configVersion;
        ConfigLoadedAtUtc = configLoadedAtUtc;
        ConfiguredListeners = configuredListeners;
        ConfiguredRoutes = configuredRoutes;
        Metrics = metrics;
        Upstreams = ProxyStatusList.Copy(upstreams);
    }

    public bool ListenerLive { get; }

    public string? ListenerName { get; }

    public string? Endpoint { get; }

    public DateTimeOffset? StartedAt { get; }

    public DateTimeOffset? StoppedAt { get; }

    public string? LastError { get; }

    public bool IsShuttingDown { get; }

    public DateTimeOffset? ShutdownStartedAtUtc { get; }

    public DateTimeOffset? ShutdownDeadlineUtc { get; }

    public int? ConfigVersion { get; }

    public DateTimeOffset? ConfigLoadedAtUtc { get; }

    public int ConfiguredListeners { get; }

    public int ConfiguredRoutes { get; }

    public ProxyMetricsSnapshot Metrics { get; }

    public IReadOnlyList<ProxyUpstreamStatus> Upstreams { get; }

    public IReadOnlyList<ProxyListenerStatus> Listeners
    {
        get => _listeners;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            _listeners = ProxyStatusList.Copy(value);
        }
    }

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
