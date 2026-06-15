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
    private IReadOnlyList<ProxyListenerStatus> _listeners = ProxyRuntimeSnapshotList.Copy<ProxyListenerStatus>([]);

    public IReadOnlyList<ProxyListenerStatus> Listeners
    {
        get => _listeners;
        init => _listeners = ProxyRuntimeSnapshotList.Copy(value);
    }

    public ProxyListenerReloadResult? LastListenerReload { get; init; }
}

internal static class ProxyRuntimeSnapshotList
{
    public static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return new System.Collections.ObjectModel.ReadOnlyCollection<T>(values.ToArray());
    }
}
