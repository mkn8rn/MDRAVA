namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeLogPersistenceOptions(
    bool AccessLogEnabled,
    bool AdminAuditEnabled,
    long MaxFileBytes,
    int MaxFiles);
