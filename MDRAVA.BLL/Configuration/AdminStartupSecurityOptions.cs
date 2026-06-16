namespace MDRAVA.BLL.Configuration;

public sealed record AdminStartupSecurityOptions
{
    public AdminStartupSecurityOptions(
        IReadOnlyList<string> Urls,
        bool RequireAuthentication,
        bool HasConfiguredToken)
    {
        this.Urls = RuntimeList.Copy(Urls);
        this.RequireAuthentication = RequireAuthentication;
        this.HasConfiguredToken = HasConfiguredToken;
    }

    public IReadOnlyList<string> Urls { get; }

    public bool RequireAuthentication { get; init; }

    public bool HasConfiguredToken { get; init; }

    public bool AuthenticationEnabled => RequireAuthentication && HasConfiguredToken;
}
