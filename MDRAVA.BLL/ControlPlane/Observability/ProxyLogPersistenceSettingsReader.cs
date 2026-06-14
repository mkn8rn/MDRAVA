using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Observability;

public static class ProxyLogPersistenceSettingsMapper
{
    public static ProxyLogPersistenceSettings FromRuntimeOptions(RuntimeLogPersistenceOptions options)
    {
        return new ProxyLogPersistenceSettings(
            options.AccessLogEnabled,
            options.AdminAuditEnabled,
            options.MaxFileBytes,
            options.MaxFiles);
    }
}

public sealed class ProxyLogPersistenceSettingsReader : IProxyLogPersistenceSettingsReader
{
    private readonly IProxyLogPersistenceSettingsSource _settingsSource;

    public ProxyLogPersistenceSettingsReader(IProxyLogPersistenceSettingsSource settingsSource)
    {
        _settingsSource = settingsSource;
    }

    public ProxyLogPersistenceSettingsReadResult ReadLogPersistenceSettings()
    {
        var result = _settingsSource.ReadLogPersistenceSettings();
        if (result is ProxyLogPersistenceSettingsSourceResult.AvailableResult available)
        {
            return ProxyLogPersistenceSettingsReadResult.Active(available.Settings);
        }

        return ProxyLogPersistenceSettingsReadResult.DisabledDefaults();
    }
}
