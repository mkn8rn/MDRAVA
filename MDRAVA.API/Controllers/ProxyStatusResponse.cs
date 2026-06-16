using BusinessProxyStatus = MDRAVA.BLL.ControlPlane.Status.ProxyStatus;

namespace MDRAVA.API.Controllers;

public sealed record ProxyStatusResponse
{
    public ProxyStatusResponse(
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
        ProxyMetricsSnapshotResponse metrics,
        IReadOnlyList<ProxyUpstreamStatusResponse> upstreams,
        IReadOnlyList<ProxyListenerStatusResponse> listeners)
    {
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
        Upstreams = ApiResponseList.Copy(upstreams);
        Listeners = ApiResponseList.Copy(listeners);
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

    public ProxyMetricsSnapshotResponse Metrics { get; }

    public IReadOnlyList<ProxyUpstreamStatusResponse> Upstreams { get; }

    public IReadOnlyList<ProxyListenerStatusResponse> Listeners { get; }

    public ProxyListenerReloadResponse? LastListenerReload { get; init; }

    public RuntimeHttp3SupportResponse Http3 { get; init; } = null!;

    public RouteDiagnosticsStatusResponse RouteDiagnostics { get; init; } = null!;

    public ConfigLintStatusResponse ConfigLint { get; init; } = null!;

    public ProxyLogPersistenceStatusResponse LogPersistence { get; init; } = null!;

    public ProxyReadinessStatusResponse Readiness { get; init; } = null!;

    public ProxySubsystemSummariesResponse Subsystems { get; init; } = null!;

    public ProxyRuntimePreflightStatusResponse RuntimePreflight { get; init; } = null!;

    public static ProxyStatusResponse FromBusinessResponse(BusinessProxyStatus response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new ProxyStatusResponse(
            listenerLive: response.ListenerLive,
            listenerName: response.ListenerName,
            endpoint: response.Endpoint,
            startedAt: response.StartedAt,
            stoppedAt: response.StoppedAt,
            lastError: response.LastError,
            isShuttingDown: response.IsShuttingDown,
            shutdownStartedAtUtc: response.ShutdownStartedAtUtc,
            shutdownDeadlineUtc: response.ShutdownDeadlineUtc,
            configVersion: response.ConfigVersion,
            configLoadedAtUtc: response.ConfigLoadedAtUtc,
            configuredListeners: response.ConfiguredListeners,
            configuredRoutes: response.ConfiguredRoutes,
            metrics: ProxyMetricsSnapshotResponse.FromSnapshot(response.Metrics),
            upstreams: ProxyUpstreamStatusResponse.FromStatuses(response.Upstreams),
            listeners: ProxyListenerStatusResponse.FromStatuses(response.Listeners))
        {
            LastListenerReload = response.LastListenerReload is null
                ? null
                : ProxyListenerReloadResponse.FromResult(response.LastListenerReload),
            Http3 = RuntimeHttp3SupportResponse.FromProjection(response.Http3),
            RouteDiagnostics = RouteDiagnosticsStatusResponse.FromStatus(response.RouteDiagnostics),
            ConfigLint = ConfigLintStatusResponse.FromStatus(response.ConfigLint),
            LogPersistence = ProxyLogPersistenceStatusResponse.FromStatus(response.LogPersistence),
            Readiness = ProxyReadinessStatusResponse.FromStatus(response.Readiness),
            Subsystems = ProxySubsystemSummariesResponse.FromSummaries(response.Subsystems),
            RuntimePreflight = ProxyRuntimePreflightStatusResponse.FromStatus(response.RuntimePreflight)
        };
    }
}
