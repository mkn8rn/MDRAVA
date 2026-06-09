namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeObservabilityOptions(
    bool AccessLogEnabled,
    int RecentDiagnosticsCapacity,
    RuntimeLogPersistenceOptions LogPersistence);
