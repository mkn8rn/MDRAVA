namespace MDRAVA.API.Models.Hosting;

public sealed record ProxyListenerStatus(
    string Name,
    string Identity,
    string BindKey,
    string Address,
    int Port,
    string Transport,
    bool TlsEnabled,
    ProxyListenerState State,
    long ActiveConnections,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? StoppedAtUtc,
    string? LastError);
