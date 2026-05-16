namespace MDRAVA.API.Models.Observability;

public sealed record AdminAuditEvent(
    DateTimeOffset TimestampUtc,
    string Method,
    string Path,
    string? ClientIp,
    string AuthResult,
    int StatusCode,
    bool Succeeded);
