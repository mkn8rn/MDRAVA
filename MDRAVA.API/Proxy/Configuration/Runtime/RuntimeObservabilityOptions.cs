namespace MDRAVA.API.Proxy.Configuration.Runtime;

public sealed record RuntimeObservabilityOptions(
    bool AccessLogEnabled,
    int RecentDiagnosticsCapacity);
