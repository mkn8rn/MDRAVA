using MDRAVA.API.Proxy.Configuration.Storage;

namespace MDRAVA.API.Proxy.Diagnostics;

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
