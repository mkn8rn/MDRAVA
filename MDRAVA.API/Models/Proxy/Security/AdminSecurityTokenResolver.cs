namespace MDRAVA.API.Proxy.Security;

public static class AdminSecurityTokenResolver
{
    public const string DefaultTokenEnvironmentVariable = "MDRAVA_ADMIN_TOKEN";

    public static RuntimeAdminSecurityOptions ToRuntimeOptions(ProxyAdminOptions options)
    {
        var resolved = Resolve(options);
        return new RuntimeAdminSecurityOptions(
            NormalizeUrls(options.Urls),
            options.RequireAuthentication,
            !string.IsNullOrEmpty(resolved.Token),
            resolved.Token,
            resolved.TokenEnvironmentVariable,
            resolved.TokenSource,
            options.RecentAuditCapacity);
    }

    public static bool IsAuthenticationEnabled(ProxyAdminOptions options)
    {
        return options.RequireAuthentication && !string.IsNullOrEmpty(Resolve(options).Token);
    }

    public static AdminTokenResolution Resolve(ProxyAdminOptions options)
    {
        var tokenEnvironmentVariable = NormalizeTokenEnvironmentVariable(options.TokenEnvironmentVariable);
        if (!string.IsNullOrEmpty(options.Token))
        {
            return new AdminTokenResolution(options.Token, tokenEnvironmentVariable, "direct");
        }

        var environmentToken = Environment.GetEnvironmentVariable(tokenEnvironmentVariable);
        return string.IsNullOrEmpty(environmentToken)
            ? new AdminTokenResolution(null, tokenEnvironmentVariable, "none")
            : new AdminTokenResolution(environmentToken, tokenEnvironmentVariable, "environment");
    }

    public static string NormalizeTokenEnvironmentVariable(string? tokenEnvironmentVariable)
    {
        return string.IsNullOrWhiteSpace(tokenEnvironmentVariable)
            ? DefaultTokenEnvironmentVariable
            : tokenEnvironmentVariable.Trim();
    }

    public static IReadOnlyList<string> NormalizeUrls(IReadOnlyList<string> urls)
    {
        return urls
            .Where(static url => !string.IsNullOrWhiteSpace(url))
            .Select(static url => url.Trim())
            .ToArray();
    }
}

public sealed record AdminTokenResolution(
    string? Token,
    string TokenEnvironmentVariable,
    string TokenSource);
