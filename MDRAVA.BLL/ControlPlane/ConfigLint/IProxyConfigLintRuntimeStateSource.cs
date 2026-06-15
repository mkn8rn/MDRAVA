using MDRAVA.BLL.ControlPlane.Listeners;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public interface IProxyConfigLintRuntimeStateSource
{
    IReadOnlyList<ProxyConfigLintRuntimeListenerState> GetListenerStates();
}

public sealed record ProxyConfigLintRuntimeListenerState(
    string Identity,
    string Kind,
    bool Active);

public static class ProxyConfigLintRuntimeListenerStateMapper
{
    public static IReadOnlyList<ProxyConfigLintRuntimeListenerState> FromListenerStatuses(
        IReadOnlyList<ProxyListenerStatus> listeners)
    {
        ArgumentNullException.ThrowIfNull(listeners);

        return listeners
            .Select(static listener => new ProxyConfigLintRuntimeListenerState(
                listener.Identity,
                listener.Kind,
                listener.State == ProxyListenerState.Active))
            .ToArray();
    }
}
