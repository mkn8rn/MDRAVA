using Microsoft.Extensions.Options;

namespace MDRAVA.API.Proxy.Configuration.Paths;

public sealed class MdravaDataDirectoryProvider : IMdravaDataDirectoryProvider
{
    public const string EnvironmentVariableName = "MDRAVA_DATA_DIR";

    private readonly MdravaDataDirectoryOptions _options;

    public MdravaDataDirectoryProvider(IOptions<MdravaDataDirectoryOptions> options)
    {
        _options = options.Value;
    }

    public string GetDataDirectory()
    {
        var environmentOverride = Environment.GetEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(environmentOverride))
        {
            return Path.GetFullPath(environmentOverride);
        }

        if (!string.IsNullOrWhiteSpace(_options.DataDirectory))
        {
            return Path.GetFullPath(_options.DataDirectory);
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return Path.Combine(localAppData, "MDRAVA");
        }

        return Path.Combine(AppContext.BaseDirectory, "MDRAVA");
    }

    public string GetProxyConfigDirectory()
    {
        return Path.Combine(GetDataDirectory(), "config");
    }

    public string GetSitesConfigDirectory()
    {
        return Path.Combine(GetProxyConfigDirectory(), "sites");
    }

    public string GetProxyOperationalConfigPath()
    {
        return Path.Combine(GetProxyConfigDirectory(), "proxy.json");
    }
}
