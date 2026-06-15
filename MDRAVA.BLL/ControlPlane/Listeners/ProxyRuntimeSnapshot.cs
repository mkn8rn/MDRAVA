namespace MDRAVA.BLL.ControlPlane.Listeners;

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
    private IReadOnlyList<ProxyListenerStatus> _listeners = ProxyListenerList.Copy<ProxyListenerStatus>([]);

    public IReadOnlyList<ProxyListenerStatus> Listeners
    {
        get => _listeners;
        init => _listeners = ProxyListenerList.Copy(value);
    }

    public ProxyListenerReloadResult? LastListenerReload { get; init; }
}
