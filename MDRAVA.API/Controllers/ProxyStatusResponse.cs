using MDRAVA.BLL.ControlPlane.ConfigLint;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Listeners;
using MDRAVA.BLL.ControlPlane.Metrics;
using MDRAVA.BLL.ControlPlane.RouteDiagnostics;
using MDRAVA.BLL.ControlPlane.Status;

using BusinessProxyStatus = MDRAVA.BLL.ControlPlane.Status.ProxyStatus;

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

    public RouteDiagnosticsStatusResponse RouteDiagnostics { get; init; } = null!;

    public ConfigLintStatus ConfigLint { get; init; } = null!;

    public ProxyLogPersistenceStatusResponse LogPersistence { get; init; } = null!;

    public ProxyReadinessStatusResponse Readiness { get; init; } = null!;

    public ProxySubsystemSummaries Subsystems { get; init; } = null!;

    public ProxyRuntimePreflightStatusResponse RuntimePreflight { get; init; } = null!;

    public static ProxyStatusResponse FromBusinessResponse(BusinessProxyStatus response)
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
            Upstreams: ProxyUpstreamStatusResponse.FromStatuses(response.Upstreams))
        {
            Listeners = response.Listeners,
            LastListenerReload = response.LastListenerReload is null
                ? null
                : ProxyListenerReloadResponse.FromResult(response.LastListenerReload),
            Http3 = response.Http3,
            RouteDiagnostics = RouteDiagnosticsStatusResponse.FromStatus(response.RouteDiagnostics),
            ConfigLint = response.ConfigLint,
            LogPersistence = ProxyLogPersistenceStatusResponse.FromStatus(response.LogPersistence),
            Readiness = ProxyReadinessStatusResponse.FromStatus(response.Readiness),
            Subsystems = response.Subsystems,
            RuntimePreflight = ProxyRuntimePreflightStatusResponse.FromStatus(response.RuntimePreflight)
        };
    }
}
