namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeLogPersistenceProjection(
    bool AccessLogEnabled,
    bool AdminAuditEnabled,
    long MaxFileBytes,
    int MaxFiles);
