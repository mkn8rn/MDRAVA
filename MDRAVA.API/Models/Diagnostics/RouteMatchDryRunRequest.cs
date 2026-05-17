namespace MDRAVA.API.Models.Diagnostics;

public sealed record RouteMatchDryRunRequest(
    string Scheme,
    string Host,
    int? Port,
    string Method,
    string Path,
    string Query,
    IReadOnlyDictionary<string, string>? Headers,
    string? ClientIp,
    string? ListenerName);
