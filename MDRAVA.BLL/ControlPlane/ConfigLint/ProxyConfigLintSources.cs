using MDRAVA.BLL.Infrastructure;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public sealed class ProxyConfigLintActiveConfigurationSource
    : IProxyConfigLintActiveConfigurationSource
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyConfigLintActiveConfigurationSource(IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public bool TryRead(out ProxyConfigLintConfigurationSnapshot? snapshot)
    {
        if (!_configurationStore.TryGetSnapshot(out var runtimeSnapshot) || runtimeSnapshot is null)
        {
            snapshot = null;
            return false;
        }

        snapshot = ProxyConfigLintConfigurationSnapshotMapper.ToLintSnapshot(runtimeSnapshot);
        return true;
    }
}

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
