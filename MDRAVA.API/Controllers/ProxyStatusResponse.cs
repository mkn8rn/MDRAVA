using MDRAVA.BLL.ControlPlane.ConfigLint;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Listeners;
using MDRAVA.BLL.ControlPlane.Metrics;
using MDRAVA.BLL.ControlPlane.RouteDiagnostics;
using MDRAVA.BLL.ControlPlane.Status;

using BusinessProxyStatusResponse = MDRAVA.BLL.ControlPlane.Status.ProxyStatusResponse;

namespace MDRAVA.API.Controllers;

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

    public ProxyListenerReloadResponse? LastListenerReload { get; init; }

    public RuntimeHttp3SupportProjection Http3 { get; init; } = null!;

    public RouteDiagnosticsStatus RouteDiagnostics { get; init; } = null!;

    public ConfigLintStatus ConfigLint { get; init; } = null!;

    public ProxyLogPersistenceStatus LogPersistence { get; init; } = null!;

    public ProxyReadinessStatus Readiness { get; init; } = null!;

    public ProxySubsystemSummaries Subsystems { get; init; } = null!;

    public ProxyRuntimePreflightStatus RuntimePreflight { get; init; } = null!;

    public static ProxyStatusResponse FromBusinessResponse(BusinessProxyStatusResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new ProxyStatusResponse(
            ListenerLive: response.ListenerLive,
            ListenerName: response.ListenerName,
            Endpoint: response.Endpoint,
            StartedAt: response.StartedAt,
            StoppedAt: response.StoppedAt,
            LastError: response.LastError,
            IsShuttingDown: response.IsShuttingDown,
            ShutdownStartedAtUtc: response.ShutdownStartedAtUtc,
            ShutdownDeadlineUtc: response.ShutdownDeadlineUtc,
            ConfigVersion: response.ConfigVersion,
            ConfigLoadedAtUtc: response.ConfigLoadedAtUtc,
            ConfiguredListeners: response.ConfiguredListeners,
            ConfiguredRoutes: response.ConfiguredRoutes,
            Metrics: response.Metrics,
            Upstreams: response.Upstreams)
        {
            Listeners = response.Listeners,
            LastListenerReload = response.LastListenerReload is null
                ? null
                : ProxyListenerReloadResponse.FromResult(response.LastListenerReload),
            Http3 = response.Http3,
            RouteDiagnostics = response.RouteDiagnostics,
            ConfigLint = response.ConfigLint,
            LogPersistence = response.LogPersistence,
            Readiness = response.Readiness,
            Subsystems = response.Subsystems,
            RuntimePreflight = response.RuntimePreflight
        };
    }
}
