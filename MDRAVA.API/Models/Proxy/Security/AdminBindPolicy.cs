using System.Net;

namespace MDRAVA.API.Proxy.Security;

public static class AdminBindPolicy
{
    public const string DefaultAdminUrl = "http://localhost:5041";
    public const string MdravaAdminUrlsConfigurationKey = "Mdrava:Admin:Urls";
    public const string AspNetCoreUrlsConfigurationKey = "urls";

    public static AdminBindResolution Apply(WebApplicationBuilder builder)
    {
        var startupSecurity = AdminStartupConfigurationReader.Read(builder.Configuration);
        var resolution = Resolve(builder.Configuration, startupSecurity);

        if (resolution.ApplyToWebHost)
        {
            builder.WebHost.UseUrls(resolution.Urls.ToArray());
        }

        return resolution;
    }

    public static AdminBindResolution Resolve(
        IConfiguration configuration,
        AdminStartupSecurityOptions startupSecurity)
    {
        var proxyConfiguredUrls = startupSecurity.Urls;
        if (proxyConfiguredUrls.Count > 0)
        {
            return Validate(
                proxyConfiguredUrls,
                "proxy-operational-config",
                applyToWebHost: true,
                startupSecurity);
        }

        var mdravaConfiguredUrls = ReadConfiguredUrls(configuration, MdravaAdminUrlsConfigurationKey);
        if (mdravaConfiguredUrls.Count > 0)
        {
            return Validate(
                mdravaConfiguredUrls,
                MdravaAdminUrlsConfigurationKey,
                applyToWebHost: true,
                startupSecurity);
        }

        var aspNetCoreUrls = ReadConfiguredUrls(configuration, AspNetCoreUrlsConfigurationKey);
        if (aspNetCoreUrls.Count > 0)
        {
            return Validate(
                aspNetCoreUrls,
                AspNetCoreUrlsConfigurationKey,
                applyToWebHost: false,
                startupSecurity);
        }

        return Validate(
            [DefaultAdminUrl],
            "default",
            applyToWebHost: true,
            startupSecurity);
    }

    public static bool IsLocalAdminUrl(string url)
    {
        if (!TryCreateAbsoluteUri(url, out var uri))
        {
            return false;
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(uri.Host, "*", StringComparison.Ordinal)
            || string.Equals(uri.Host, "+", StringComparison.Ordinal))
        {
            return false;
        }

        return IPAddress.TryParse(uri.Host.Trim('[', ']'), out var address)
            && IPAddress.IsLoopback(address);
    }

    public static bool IsValidAdminUrl(string url)
    {
        return TryCreateAbsoluteUri(url, out _);
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

    private static IReadOnlyList<string> ReadConfiguredUrls(IConfiguration configuration, string key)
    {
        var section = configuration.GetSection(key);
        var children = section.GetChildren()
            .Select(static child => child.Value)
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim())
            .ToArray();

        if (children.Length > 0)
        {
            return children;
        }

        var value = section.Value ?? configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool TryCreateAbsoluteUri(string url, out Uri uri)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out uri!))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record AdminBindResolution(
    IReadOnlyList<string> Urls,
    string Source,
    bool ApplyToWebHost,
    bool IsLocalOnly,
    bool RequireAuthentication,
    bool HasConfiguredToken);
