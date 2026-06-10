using MDRAVA.BLL.Configuration;
using MDRAVA.INF.Configuration;
using MDRAVA.INF.Configuration.Loading;
using MDRAVA.INF.Configuration.Paths;

namespace MDRAVA.API.Proxy.Security;

public static class AdminBindWebHostConfigurator
{
    public const string MdravaAdminUrlsConfigurationKey = "Mdrava:Admin:Urls";
    public const string AspNetCoreUrlsConfigurationKey = "urls";

    public static AdminBindResolution Apply(WebApplicationBuilder builder)
    {
        var dataOptions = new MdravaDataDirectoryOptions();
        builder.Configuration.GetSection(MdravaDataDirectoryOptions.SectionName).Bind(dataOptions);

        var startupSecurity = AdminStartupConfigurationReader.Read(dataOptions);
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
        return AdminBindPolicy.Resolve(
            new AdminBindPolicyInput(
                [
                    new AdminBindCandidate(
                        startupSecurity.Urls,
                        "proxy-operational-config",
                        ApplyToWebHost: true),
                    new AdminBindCandidate(
                        ReadConfiguredUrls(configuration, MdravaAdminUrlsConfigurationKey),
                        MdravaAdminUrlsConfigurationKey,
                        ApplyToWebHost: true),
                    new AdminBindCandidate(
                        ReadConfiguredUrls(configuration, AspNetCoreUrlsConfigurationKey),
                        AspNetCoreUrlsConfigurationKey,
                        ApplyToWebHost: false)
                ],
                startupSecurity),
            new ProxyAdminUrlPolicy());
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
}
