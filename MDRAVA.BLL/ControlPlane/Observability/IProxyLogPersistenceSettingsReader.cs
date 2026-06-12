namespace MDRAVA.BLL.ControlPlane.Observability;

public interface IProxyLogPersistenceSettingsReader
{
    bool TryGetLogPersistenceSettings(out ProxyLogPersistenceSettings settings);
}

public interface IProxyLogPersistenceSettingsSource
{
    bool TryGetLogPersistenceSettings(out ProxyLogPersistenceSettings settings);
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
