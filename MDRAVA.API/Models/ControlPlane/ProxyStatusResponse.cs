using MDRAVA.API.Proxy.Metrics;

namespace MDRAVA.API.Models.ControlPlane;

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
}
