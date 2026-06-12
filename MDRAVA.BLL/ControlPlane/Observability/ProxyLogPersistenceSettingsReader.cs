using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.BLL.ControlPlane.Observability;

public sealed class ProxyLogPersistenceSettingsReader : IProxyLogPersistenceSettingsReader
{
    private readonly IProxyLogPersistenceSettingsSource _settingsSource;

    public ProxyLogPersistenceSettingsReader(IProxyLogPersistenceSettingsSource settingsSource)
    {
        _settingsSource = settingsSource;
    }

    public bool TryGetLogPersistenceSettings(out ProxyLogPersistenceSettings settings)
    {
        if (_settingsSource.TryGetLogPersistenceSettings(out settings))
        {
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

public sealed class ProxyConfigurationLogPersistenceSettingsSource
    : IProxyLogPersistenceSettingsSource
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyConfigurationLogPersistenceSettingsSource(IProxyConfigurationStore configurationStore)
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
            MaxFileBytes: 0,
            MaxFiles: 0);
        return false;
    }
}
