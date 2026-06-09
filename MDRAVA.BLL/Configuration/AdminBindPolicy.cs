namespace MDRAVA.BLL.Configuration;

public static class AdminBindPolicy
{
    public const string DefaultAdminUrl = "http://localhost:5041";

    public static AdminBindResolution Resolve(AdminBindPolicyInput input)
    {
        foreach (var candidate in input.Candidates)
        {
            if (candidate.Urls.Count == 0)
            {
                continue;
            }

            return Validate(
                candidate.Urls,
                candidate.Source,
                candidate.ApplyToWebHost,
                input.StartupSecurity);
        }

        return Validate(
            [DefaultAdminUrl],
            "default",
            applyToWebHost: true,
            input.StartupSecurity);
    }

    public static bool IsLocalAdminUrl(string url)
    {
        return ProxyAdminUrlPolicy.IsLocal(url);
    }

    public static bool IsValidAdminUrl(string url)
    {
        return ProxyAdminUrlPolicy.IsValid(url);
    }

    private static AdminBindResolution Validate(
        IReadOnlyList<string> urls,
        string source,
        bool applyToWebHost,
        AdminStartupSecurityOptions startupSecurity)
    {
        var normalizedUrls = urls
            .Where(static url => !string.IsNullOrWhiteSpace(url))
            .Select(static url => url.Trim())
            .ToArray();

        if (normalizedUrls.Length == 0)
        {
            throw new InvalidOperationException("MDRAVA admin bind configuration did not contain any URLs.");
        }

        foreach (var url in normalizedUrls)
        {
            if (!IsValidAdminUrl(url))
            {
                throw new InvalidOperationException($"MDRAVA admin bind URL '{url}' is invalid. Use an absolute http or https URL.");
            }
        }

        var isLocalOnly = normalizedUrls.All(IsLocalAdminUrl);
        if (!isLocalOnly && !startupSecurity.AuthenticationEnabled)
        {
            throw new InvalidOperationException(
                "MDRAVA admin API is configured to bind a non-local URL, but admin authentication is not enabled with a configured token. "
                + "Set proxy operational config admin.requireAuthentication to true and provide admin.token or the configured token environment variable.");
        }

        return new AdminBindResolution(
            normalizedUrls,
            source,
            applyToWebHost,
            isLocalOnly,
            startupSecurity.RequireAuthentication,
            startupSecurity.HasConfiguredToken);
    }

}

public sealed record AdminBindPolicyInput(
    IReadOnlyList<AdminBindCandidate> Candidates,
    AdminStartupSecurityOptions StartupSecurity);

public sealed record AdminBindCandidate(
    IReadOnlyList<string> Urls,
    string Source,
    bool ApplyToWebHost);

public sealed record AdminBindResolution(
    IReadOnlyList<string> Urls,
    string Source,
    bool ApplyToWebHost,
    bool IsLocalOnly,
    bool RequireAuthentication,
    bool HasConfiguredToken);
