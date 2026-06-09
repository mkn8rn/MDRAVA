namespace MDRAVA.BLL.Configuration;

public sealed record AdminStartupSecurityOptions(
    IReadOnlyList<string> Urls,
    bool RequireAuthentication,
    bool HasConfiguredToken)
{
    public bool AuthenticationEnabled => RequireAuthentication && HasConfiguredToken;
}
