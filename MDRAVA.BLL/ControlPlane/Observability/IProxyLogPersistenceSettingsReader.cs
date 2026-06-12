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
    int MaxFiles);
