namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeObservabilityOptions(
    bool AccessLogEnabled,
    int RecentDiagnosticsCapacity,
    RuntimeLogPersistenceOptions LogPersistence);
