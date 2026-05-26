using MDRAVA.API.Proxy.Hosting;

namespace MDRAVA.API.Proxy.Diagnostics;

public sealed class ProxyConfigLintRuntimeStateSource : IProxyConfigLintRuntimeStateSource
{
    private readonly ProxyRuntimeState _runtimeState;

    public ProxyConfigLintRuntimeStateSource(ProxyRuntimeState runtimeState)
    {
        _runtimeState = runtimeState;
    }

    public IReadOnlyList<ProxyListenerStatus> GetListeners()
    {
        return _runtimeState.Snapshot().Listeners;
    }
}
