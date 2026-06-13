namespace MDRAVA.BLL.ControlPlane.Observability;

public interface IProxyLogPersistenceSettingsReader
{
    ProxyLogPersistenceSettingsReadResult ReadLogPersistenceSettings();
}

public interface IProxyLogPersistenceSettingsSource
{
    ProxyLogPersistenceSettingsSourceResult ReadLogPersistenceSettings();
}

public sealed record ProxyLogPersistenceSettings(
    bool AccessLogEnabled,
    bool AdminAuditEnabled,
    long MaxFileBytes,
    int MaxFiles)
{
    public static ProxyLogPersistenceSettings DisabledOperationalDefaults { get; } = new(
        AccessLogEnabled: false,
        AdminAuditEnabled: false,
        MaxFileBytes: 1_048_576,
        MaxFiles: 8);

    public static ProxyLogPersistenceSettings Unavailable { get; } = new(
        AccessLogEnabled: false,
        AdminAuditEnabled: false,
        MaxFileBytes: 0,
        MaxFiles: 0);
}

public abstract record ProxyLogPersistenceSettingsSourceResult
{
    private ProxyLogPersistenceSettingsSourceResult()
    {
    }

    public static ProxyLogPersistenceSettingsSourceResult MissingConfiguration { get; } = new MissingConfigurationResult();

    public static ProxyLogPersistenceSettingsSourceResult Available(ProxyLogPersistenceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new AvailableResult(settings);
    }

    public sealed record AvailableResult : ProxyLogPersistenceSettingsSourceResult
    {
        public AvailableResult(ProxyLogPersistenceSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            Settings = settings;
        }

        public ProxyLogPersistenceSettings Settings { get; }
    }

    public sealed record MissingConfigurationResult : ProxyLogPersistenceSettingsSourceResult;
}

public abstract record ProxyLogPersistenceSettingsReadResult
{
    private ProxyLogPersistenceSettingsReadResult(ProxyLogPersistenceSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Settings = settings;
    }

    public ProxyLogPersistenceSettings Settings { get; }

    public static ProxyLogPersistenceSettingsReadResult Active(ProxyLogPersistenceSettings settings)
    {
        return new ActiveResult(settings);
    }

    public static ProxyLogPersistenceSettingsReadResult DisabledDefaults()
    {
        return new DisabledDefaultsResult(ProxyLogPersistenceSettings.DisabledOperationalDefaults);
    }

    public sealed record ActiveResult : ProxyLogPersistenceSettingsReadResult
    {
        public ActiveResult(ProxyLogPersistenceSettings settings)
            : base(settings)
        {
        }
    }

    public sealed record DisabledDefaultsResult : ProxyLogPersistenceSettingsReadResult
    {
        public DisabledDefaultsResult(ProxyLogPersistenceSettings settings)
            : base(settings)
        {
        }
    }
}
