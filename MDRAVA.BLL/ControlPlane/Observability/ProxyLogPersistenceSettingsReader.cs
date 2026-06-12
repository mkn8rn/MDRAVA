using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.BLL.ControlPlane.Observability;

public sealed class ProxyLogPersistenceSettingsReader : IProxyLogPersistenceSettingsReader
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyLogPersistenceSettingsReader(IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public bool TryGetLogPersistenceSettings(out ProxyLogPersistenceSettings settings)
    {
        if (_configurationStore.TryGetSnapshot(out var snapshot) && snapshot is not null)
        {
            var runtimeOptions = snapshot.Observability.LogPersistence;
            settings = new ProxyLogPersistenceSettings(
                runtimeOptions.AccessLogEnabled,
                runtimeOptions.AdminAuditEnabled,
                runtimeOptions.MaxFileBytes,
                runtimeOptions.MaxFiles);
            return true;
        }

        settings = new ProxyLogPersistenceSettings(
            AccessLogEnabled: false,
            AdminAuditEnabled: false,
            MaxFileBytes: 1_048_576,
            MaxFiles: 8);
        return false;
    }
}
