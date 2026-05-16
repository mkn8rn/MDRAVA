using MDRAVA.API.Proxy.Configuration.Paths;

namespace MDRAVA.API.Proxy.Configuration.Loading;

public sealed class ProxyDataDirectoryBootstrapper
{
    public const string ExampleSiteFileName = "example.site.yaml";

    private readonly IMdravaDataDirectoryProvider _dataDirectoryProvider;

    public ProxyDataDirectoryBootstrapper(IMdravaDataDirectoryProvider dataDirectoryProvider)
    {
        _dataDirectoryProvider = dataDirectoryProvider;
    }

    public ProxyConfigurationDiscovery EnsureLayout()
    {
        var layout = new ProxyFilesystemLayout(
            _dataDirectoryProvider.GetDataDirectory(),
            _dataDirectoryProvider.GetProxyConfigDirectory(),
            _dataDirectoryProvider.GetSitesConfigDirectory(),
            _dataDirectoryProvider.GetLogsDirectory(),
            _dataDirectoryProvider.GetCertificatesDirectory(),
            _dataDirectoryProvider.GetStateDirectory(),
            _dataDirectoryProvider.GetProxyOperationalConfigPath());

        List<string> created = [];
        List<string> existing = [];

        EnsureDirectory(layout.DataDirectory, created, existing);
        EnsureDirectory(layout.ConfigDirectory, created, existing);
        EnsureDirectory(layout.SitesDirectory, created, existing);
        EnsureDirectory(layout.LogsDirectory, created, existing);
        EnsureDirectory(layout.CertificatesDirectory, created, existing);
        EnsureDirectory(layout.StateDirectory, created, existing);

        EnsureFile(layout.ProxyConfigPath, "{}" + Environment.NewLine, created, existing);
        EnsureFile(
            Path.Combine(layout.SitesDirectory, ExampleSiteFileName),
            ExampleSiteText(),
            created,
            existing);

        return new ProxyConfigurationDiscovery(layout, [], created, existing);
    }

    private static void EnsureDirectory(string path, List<string> created, List<string> existing)
    {
        if (Directory.Exists(path))
        {
            existing.Add(path);
            return;
        }

        Directory.CreateDirectory(path);
        created.Add(path);
    }

    private static void EnsureFile(string path, string content, List<string> created, List<string> existing)
    {
        if (File.Exists(path))
        {
            existing.Add(path);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        created.Add(path);
    }

    private static string ExampleSiteText()
    {
        return """
        # This file is a safe placeholder and is ignored by MDRAVA because its
        # name is example.site.yaml. Copy it to another .yaml or .yml file name
        # in this directory before enabling a real site.
        #
        # Example active config:
        #
        # name: example
        # listeners:
        #   - name: main
        #     address: 127.0.0.1
        #     port: 8080
        #     enabled: false
        # host: example.test
        # routes:
        #   - name: app
        #     pathPrefix: /
        #     action: proxy
        #     upstreams:
        #       - name: local-app
        #         address: 127.0.0.1
        #         port: 5000
        #
        """ + Environment.NewLine;
    }
}
