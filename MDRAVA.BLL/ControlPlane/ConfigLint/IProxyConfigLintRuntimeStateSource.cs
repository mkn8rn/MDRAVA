using MDRAVA.BLL.ControlPlane.Listeners;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public interface IProxyConfigLintRuntimeStateSource
{
    IReadOnlyList<ProxyConfigLintRuntimeListenerState> GetListenerStates();
}

public sealed record ProxyConfigLintRuntimeListenerState
{
    public ProxyConfigLintRuntimeListenerState(
        string identity,
        string kind,
        bool active)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);

        Identity = identity;
        Kind = kind;
        Active = active;
    }

    public string Identity { get; }

    public string Kind { get; }

    public bool Active { get; }
}

public static class ProxyConfigLintRuntimeListenerStateMapper
{
    public static IReadOnlyList<ProxyConfigLintRuntimeListenerState> FromListenerStatuses(
        IReadOnlyList<ProxyListenerStatus> listeners)
    {
        ArgumentNullException.ThrowIfNull(listeners);

        return ConfigLintList.Copy(listeners.Select(ToListenerState));
    }

    private static ProxyConfigLintRuntimeListenerState ToListenerState(ProxyListenerStatus listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        return new ProxyConfigLintRuntimeListenerState(
            listener.Identity,
            listener.Kind,
            listener.State == ProxyListenerState.Active);
    }
}
