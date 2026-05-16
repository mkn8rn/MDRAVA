using System.Text.Json;
using MDRAVA.API.Proxy.Configuration.Loading;
using MDRAVA.API.Proxy.Configuration.Paths;
using Microsoft.Extensions.Options;

namespace MDRAVA.API.Proxy.Security;

public static class AdminStartupConfigurationReader
{
    public static AdminStartupSecurityOptions Read(IConfiguration configuration)
    {
        var dataOptions = new MdravaDataDirectoryOptions();
        configuration.GetSection(MdravaDataDirectoryOptions.SectionName).Bind(dataOptions);

        var dataDirectory = new MdravaDataDirectoryProvider(Options.Create(dataOptions));
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

            var token = AdminSecurityTokenResolver.Resolve(options.Admin).Token;
            return new AdminStartupSecurityOptions(
                AdminSecurityTokenResolver.NormalizeUrls(options.Admin.Urls),
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
