using MDRAVA.API.Proxy.Configuration.Storage;

namespace MDRAVA.API.Proxy.Observability;

public sealed class ProxyLogPersistenceSettingsReader : IProxyLogPersistenceSettingsReader
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyLogPersistenceSettingsReader(IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public bool TryGetLogPersistenceOptions(out ProxyLogPersistenceOptions options)
    {
        if (_configurationStore.TryGetSnapshot(out var snapshot) && snapshot is not null)
        {
            var runtimeOptions = snapshot.Observability.LogPersistence;
            options = new ProxyLogPersistenceOptions
            {
                AccessLogEnabled = runtimeOptions.AccessLogEnabled,
                AdminAuditEnabled = runtimeOptions.AdminAuditEnabled,
                MaxFileBytes = runtimeOptions.MaxFileBytes,
                MaxFiles = runtimeOptions.MaxFiles
            };
            return true;
        }

        options = new ProxyLogPersistenceOptions
        {
            AccessLogEnabled = false,
            AdminAuditEnabled = false,
            MaxFileBytes = 1_048_576,
            MaxFiles = 8
        };
        return false;
    }
}
