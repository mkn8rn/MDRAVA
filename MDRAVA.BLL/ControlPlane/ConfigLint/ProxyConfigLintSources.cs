using MDRAVA.BLL.Infrastructure;
using MDRAVA.BLL.ControlPlane.Listeners;
using MDRAVA.BLL.ControlPlane.Http3;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public sealed class ProxyConfigLintActiveConfigurationSource
    : IProxyConfigLintActiveConfigurationSource
{
    private readonly IProxyConfigurationStore _configurationStore;
    private readonly IRuntimeHttp3PlatformSupportSource _http3PlatformSupportSource;

    public ProxyConfigLintActiveConfigurationSource(
        IProxyConfigurationStore configurationStore,
        IRuntimeHttp3PlatformSupportSource http3PlatformSupportSource)
    {
        _configurationStore = configurationStore;
        _http3PlatformSupportSource = http3PlatformSupportSource;
    }

    public bool TryRead(out ProxyConfigLintConfigurationSnapshot? snapshot)
    {
        if (!_configurationStore.TryGetSnapshot(out var runtimeSnapshot) || runtimeSnapshot is null)
        {
            snapshot = null;
            return false;
        }

        snapshot = ProxyConfigLintConfigurationSnapshotMapper.ToLintSnapshot(
            runtimeSnapshot,
            _http3PlatformSupportSource.Read());
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
