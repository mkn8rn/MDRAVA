using MDRAVA.BLL.ControlPlane.ConfigLint;
using MDRAVA.BLL.ControlPlane.Listeners;

namespace MDRAVA.INF.Proxy.ConfigLint;

public sealed class ProxyConfigLintRuntimeStateSource : IProxyConfigLintRuntimeStateSource
{
    private readonly ProxyRuntimeState _runtimeState;

    public ProxyConfigLintRuntimeStateSource(ProxyRuntimeState runtimeState)
    {
        _runtimeState = runtimeState;
    }

    public IReadOnlyList<ProxyConfigLintRuntimeListenerState> GetListenerStates()
    {
        return _runtimeState
            .Snapshot()
            .Listeners
            .Select(static listener => new ProxyConfigLintRuntimeListenerState(
                listener.Identity,
                listener.Kind,
                listener.State == ProxyListenerState.Active))
            .ToArray();
    }
}
