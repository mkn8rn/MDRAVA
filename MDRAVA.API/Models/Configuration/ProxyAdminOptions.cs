namespace MDRAVA.API.Models.Configuration;

public sealed class ProxyAdminOptions
{
    public List<string> Urls { get; init; } = [];

    public bool RequireAuthentication { get; init; }

    public string? Token { get; init; }

    public string TokenEnvironmentVariable { get; init; } = "MDRAVA_ADMIN_TOKEN";

    public int RecentAuditCapacity { get; init; } = 200;
}
