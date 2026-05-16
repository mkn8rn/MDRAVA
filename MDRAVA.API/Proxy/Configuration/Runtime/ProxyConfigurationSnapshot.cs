namespace MDRAVA.API.Proxy.Configuration.Runtime;

public sealed record ProxyConfigurationSnapshot(
    int Version,
    DateTimeOffset LoadedAtUtc,
    string SourceDirectory,
    IReadOnlyList<string> SourceFiles,
    RuntimeTimeouts Timeouts,
    IReadOnlyList<RuntimeListener> Listeners,
    IReadOnlyList<RuntimeRoute> Routes)
{
    public RuntimeListener GetFirstEnabledListener()
    {
        return Listeners.First(static listener => listener.Enabled);
    }
}
