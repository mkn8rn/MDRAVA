namespace MDRAVA.API.Proxy.Configuration;

public sealed class ProxyObservabilityOptions
{
    public bool AccessLogEnabled { get; init; } = true;

    public int RecentDiagnosticsCapacity { get; init; } = 500;
}
