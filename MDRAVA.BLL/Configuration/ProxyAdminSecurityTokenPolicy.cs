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
            return ProxyAdminTokenResolution.Direct(options.Token, tokenEnvironmentVariable);
        }

        var environmentToken = readEnvironmentVariable(tokenEnvironmentVariable);
        return string.IsNullOrEmpty(environmentToken)
            ? ProxyAdminTokenResolution.None(tokenEnvironmentVariable)
            : ProxyAdminTokenResolution.Environment(environmentToken, tokenEnvironmentVariable);
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

public sealed record ProxyAdminTokenResolution
{
    private ProxyAdminTokenResolution(string? token, string tokenEnvironmentVariable, string tokenSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenEnvironmentVariable);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenSource);

        Token = token;
        TokenEnvironmentVariable = tokenEnvironmentVariable;
        TokenSource = tokenSource;
    }

    public string? Token { get; }

    public string TokenEnvironmentVariable { get; }

    public string TokenSource { get; }

    public static ProxyAdminTokenResolution Direct(string token, string tokenEnvironmentVariable)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        return new ProxyAdminTokenResolution(
            token: token,
            tokenEnvironmentVariable: tokenEnvironmentVariable,
            tokenSource: "direct");
    }

    public static ProxyAdminTokenResolution Environment(string token, string tokenEnvironmentVariable)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        return new ProxyAdminTokenResolution(
            token: token,
            tokenEnvironmentVariable: tokenEnvironmentVariable,
            tokenSource: "environment");
    }

    public static ProxyAdminTokenResolution None(string tokenEnvironmentVariable)
    {
        return new ProxyAdminTokenResolution(
            token: null,
            tokenEnvironmentVariable: tokenEnvironmentVariable,
            tokenSource: "none");
    }
}
