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
        return ProxyConfigLintRuntimeListenerStateMapper.FromListenerStatuses(
            _runtimeState.Snapshot().Listeners);
    }
}
