namespace MDRAVA.API.Models.Hosting;

public sealed record ProxyRuntimeSnapshot(
    bool IsRunning,
    string? ListenerName,
    string? Endpoint,
    DateTimeOffset? StartedAt,
    DateTimeOffset? StoppedAt,
    string? LastError,
    bool IsShuttingDown = false,
    DateTimeOffset? ShutdownStartedAtUtc = null,
    DateTimeOffset? ShutdownDeadlineUtc = null)
{
    public IReadOnlyList<ProxyListenerStatus> Listeners { get; init; } = [];

    public ProxyListenerReloadResult? LastListenerReload { get; init; }
}
