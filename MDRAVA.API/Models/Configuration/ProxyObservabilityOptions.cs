namespace MDRAVA.API.Models.Configuration;

public sealed class ProxyObservabilityOptions
{
    public bool AccessLogEnabled { get; init; } = true;

    public int RecentDiagnosticsCapacity { get; init; } = 500;
}
