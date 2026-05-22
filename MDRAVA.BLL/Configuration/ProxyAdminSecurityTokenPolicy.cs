namespace MDRAVA.BLL.Configuration;

public static class ProxyAdminSecurityTokenPolicy
{
    public const string DefaultTokenEnvironmentVariable = "MDRAVA_ADMIN_TOKEN";

    public static bool IsAuthenticationEnabled(
        ProxyAdminOptions options,
        Func<string, string?> readEnvironmentVariable)
    {
        return options.RequireAuthentication
            && !string.IsNullOrEmpty(Resolve(options, readEnvironmentVariable).Token);
    }

    public static ProxyAdminTokenResolution Resolve(
        ProxyAdminOptions options,
        Func<string, string?> readEnvironmentVariable)
    {
        var tokenEnvironmentVariable = NormalizeTokenEnvironmentVariable(options.TokenEnvironmentVariable);
        if (!string.IsNullOrEmpty(options.Token))
        {
            return new ProxyAdminTokenResolution(options.Token, tokenEnvironmentVariable, "direct");
        }

        var environmentToken = readEnvironmentVariable(tokenEnvironmentVariable);
        return string.IsNullOrEmpty(environmentToken)
            ? new ProxyAdminTokenResolution(null, tokenEnvironmentVariable, "none")
            : new ProxyAdminTokenResolution(environmentToken, tokenEnvironmentVariable, "environment");
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

public sealed record ProxyAdminTokenResolution(
    string? Token,
    string TokenEnvironmentVariable,
    string TokenSource);
