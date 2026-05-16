namespace MDRAVA.API.Proxy.Configuration.Paths;

public interface IMdravaDataDirectoryProvider
{
    string GetDataDirectory();

    string GetProxyConfigDirectory();

    string GetProxyOperationalConfigPath();

    string GetSitesConfigDirectory();
}
