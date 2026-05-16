namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeAdminSecurityProjection(
    IReadOnlyList<string> Urls,
    bool RequireAuthentication,
    bool HasConfiguredToken,
    string? Token,
    string TokenEnvironmentVariable,
    string TokenSource,
    int RecentAuditCapacity);
