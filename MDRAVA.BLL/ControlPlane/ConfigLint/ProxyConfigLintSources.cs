using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
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

    public ProxyConfigLintActiveConfigurationReadResult Read()
    {
        if (!_configurationStore.TryGetSnapshot(out var runtimeSnapshot) || runtimeSnapshot is null)
        {
            return ProxyConfigLintActiveConfigurationReadResult.MissingConfiguration;
        }

        return ProxyConfigLintActiveConfigurationReadResult.Available(
            ProxyConfigLintConfigurationSnapshotMapper.ToLintSnapshot(
                ProxyConfigLintRuntimeConfigurationSourceMapper.FromConfiguration(runtimeSnapshot),
                _http3PlatformSupportSource.Read()));
    }
}

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
