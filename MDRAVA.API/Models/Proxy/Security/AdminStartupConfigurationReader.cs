using System.Text.Json;
using MDRAVA.INF.Configuration.Paths;

namespace MDRAVA.API.Proxy.Security;

public static class AdminStartupConfigurationReader
{
    public static AdminStartupSecurityOptions Read(IConfiguration configuration)
    {
        var dataOptions = new MdravaDataDirectoryOptions();
        configuration.GetSection(MdravaDataDirectoryOptions.SectionName).Bind(dataOptions);

        var dataDirectory = new MdravaDataDirectoryProvider(dataOptions);
        var proxyConfigPath = dataDirectory.GetProxyOperationalConfigPath();

        if (!File.Exists(proxyConfigPath))
        {
            return new AdminStartupSecurityOptions([], false, false);
        }

        try
        {
            using var stream = File.OpenRead(proxyConfigPath);
            var options = JsonSerializer.Deserialize<ProxyOperationalOptions>(
                stream,
                SiteConfigurationParser.ReadJsonOptions);

            if (options is null)
            {
                return new AdminStartupSecurityOptions([], false, false);
            }

            var token = ProxyAdminSecurityTokenPolicy.Resolve(
                options.Admin,
                Environment.GetEnvironmentVariable).Token;
            return new AdminStartupSecurityOptions(
                ProxyAdminSecurityTokenPolicy.NormalizeUrls(options.Admin.Urls),
                options.Admin.RequireAuthentication,
                !string.IsNullOrEmpty(token));
        }
        catch (JsonException)
        {
            return new AdminStartupSecurityOptions([], false, false);
        }
        catch (IOException)
        {
            return new AdminStartupSecurityOptions([], false, false);
        }
        catch (UnauthorizedAccessException)
        {
            return new AdminStartupSecurityOptions([], false, false);
        }
    }
}
