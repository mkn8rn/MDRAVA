namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeLogPersistenceOptions(
    bool AccessLogEnabled,
    bool AdminAuditEnabled,
    long MaxFileBytes,
    int MaxFiles);
