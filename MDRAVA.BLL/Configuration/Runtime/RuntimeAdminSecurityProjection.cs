namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeAdminSecurityProjection
{
    public RuntimeAdminSecurityProjection(
        IReadOnlyList<string> Urls,
        bool RequireAuthentication,
        bool HasConfiguredToken,
        string? Token,
        string TokenEnvironmentVariable,
        string TokenSource,
        int RecentAuditCapacity)
    {
        this.Urls = RuntimeList.Copy(Urls);
        this.RequireAuthentication = RequireAuthentication;
        this.HasConfiguredToken = HasConfiguredToken;
        this.Token = Token;
        this.TokenEnvironmentVariable = TokenEnvironmentVariable;
        this.TokenSource = TokenSource;
        this.RecentAuditCapacity = RecentAuditCapacity;
    }

    public IReadOnlyList<string> Urls { get; }

    public bool RequireAuthentication { get; init; }

    public bool HasConfiguredToken { get; init; }

    public string? Token { get; init; }

    public string TokenEnvironmentVariable { get; init; }

    public string TokenSource { get; init; }

    public int RecentAuditCapacity { get; init; }
}
