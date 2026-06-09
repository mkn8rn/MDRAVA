using MDRAVA.BLL.Configuration;

namespace MDRAVA.API.Proxy.Security;

public static class AdminSecurityTokenResolver
{
    public const string DefaultTokenEnvironmentVariable = ProxyAdminSecurityTokenPolicy.DefaultTokenEnvironmentVariable;

    public static RuntimeAdminSecurityOptions ToRuntimeOptions(ProxyAdminOptions options)
    {
        var resolved = Resolve(options);
        return ProxyConfigurationRuntimeMapper.ToRuntimeAdminSecurityOptions(options, resolved);
    }

    public static bool IsAuthenticationEnabled(ProxyAdminOptions options)
    {
        return ProxyAdminSecurityTokenPolicy.IsAuthenticationEnabled(options, Environment.GetEnvironmentVariable);
    }

    public static ProxyAdminTokenResolution Resolve(ProxyAdminOptions options)
    {
        return ProxyAdminSecurityTokenPolicy.Resolve(options, Environment.GetEnvironmentVariable);
    }

    public static string NormalizeTokenEnvironmentVariable(string? tokenEnvironmentVariable)
    {
        return ProxyAdminSecurityTokenPolicy.NormalizeTokenEnvironmentVariable(tokenEnvironmentVariable);
    }

    public static IReadOnlyList<string> NormalizeUrls(IReadOnlyList<string> urls)
    {
        return ProxyAdminSecurityTokenPolicy.NormalizeUrls(urls);
    }
}
