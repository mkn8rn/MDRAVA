namespace MDRAVA.API.Proxy.Hosting;

public sealed record ProxyRuntimeSnapshot(
    bool IsRunning,
    string? ListenerName,
    string? Endpoint,
    DateTimeOffset? StartedAt,
    DateTimeOffset? StoppedAt,
    string? LastError);
