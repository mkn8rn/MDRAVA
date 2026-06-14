using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using MDRAVA.BLL.ControlPlane.Observability;

namespace MDRAVA.INF.Observability;

public sealed class ProxyConfigurationLogPersistenceSettingsSource
    : IProxyLogPersistenceSettingsSource
{
    private readonly IProxyActiveConfigurationSnapshotReader _configurationStore;

    public ProxyConfigurationLogPersistenceSettingsSource(IProxyActiveConfigurationSnapshotReader configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public ProxyLogPersistenceSettingsSourceResult ReadLogPersistenceSettings()
    {
        if (_configurationStore.ReadSnapshot() is ProxyConfigurationSnapshotReadResult.AvailableResult available)
        {
            return ProxyLogPersistenceSettingsSourceResult.Available(
                ProxyLogPersistenceSettingsMapper.FromRuntimeOptions(available.Snapshot.Observability.LogPersistence));
        }

        return ProxyLogPersistenceSettingsSourceResult.MissingConfiguration;
    }
}
