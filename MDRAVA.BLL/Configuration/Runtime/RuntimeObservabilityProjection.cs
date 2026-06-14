namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeObservabilityProjection(
    bool AccessLogEnabled,
    int RecentDiagnosticsCapacity,
    RuntimeLogPersistenceProjection LogPersistence);
