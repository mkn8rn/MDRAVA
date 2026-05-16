namespace MDRAVA.API.Proxy.Configuration.Runtime;

public sealed record ProxyConfigurationProjection(
    int Version,
    DateTimeOffset LoadedAtUtc,
    string SourceDirectory,
    IReadOnlyList<string> SourceFiles,
    RuntimeTimeouts Timeouts,
    IReadOnlyList<RuntimeListener> Listeners,
    IReadOnlyList<RuntimeRoute> Routes);
