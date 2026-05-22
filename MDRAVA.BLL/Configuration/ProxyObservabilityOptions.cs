namespace MDRAVA.BLL.Configuration;

public sealed class ProxyObservabilityOptions
{
    public bool AccessLogEnabled { get; init; } = true;

    public int RecentDiagnosticsCapacity { get; init; } = 500;

    public ProxyLogPersistenceOptions LogPersistence { get; init; } = new();
}
