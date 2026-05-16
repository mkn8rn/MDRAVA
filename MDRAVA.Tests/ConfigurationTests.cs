using MDRAVA.API.Proxy.Configuration.Loading;
using MDRAVA.API.Proxy.Configuration.Paths;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Configuration.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace MDRAVA.Tests;

internal static class ConfigurationTests
{
    public static void DataDirectoryUsesConfiguredOverride()
    {
        var expected = Path.Combine(Path.GetTempPath(), $"mdrava-test-{Guid.NewGuid():N}");
        var provider = new MdravaDataDirectoryProvider(Options.Create(new MdravaDataDirectoryOptions
        {
            DataDirectory = expected
        }));

        AssertEx.Equal(Path.GetFullPath(expected), provider.GetDataDirectory());
        AssertEx.Equal(Path.Combine(Path.GetFullPath(expected), "config"), provider.GetProxyConfigDirectory());
        AssertEx.Equal(Path.Combine(Path.GetFullPath(expected), "config", "sites"), provider.GetSitesConfigDirectory());
    }

    public static void DataDirectoryUsesEnvironmentOverride()
    {
        var previous = Environment.GetEnvironmentVariable(MdravaDataDirectoryProvider.EnvironmentVariableName);
        var expected = Path.Combine(Path.GetTempPath(), $"mdrava-env-{Guid.NewGuid():N}");

        try
        {
            Environment.SetEnvironmentVariable(MdravaDataDirectoryProvider.EnvironmentVariableName, expected);
            var provider = new MdravaDataDirectoryProvider(Options.Create(new MdravaDataDirectoryOptions
            {
                DataDirectory = Path.Combine(Path.GetTempPath(), "ignored")
            }));

            AssertEx.Equal(Path.GetFullPath(expected), provider.GetDataDirectory());
        }
        finally
        {
            Environment.SetEnvironmentVariable(MdravaDataDirectoryProvider.EnvironmentVariableName, previous);
        }
    }

    public static void DataDirectoryDefaultsUnderLocalApplicationDataWhenAvailable()
    {
        var previous = Environment.GetEnvironmentVariable(MdravaDataDirectoryProvider.EnvironmentVariableName);

        try
        {
            Environment.SetEnvironmentVariable(MdravaDataDirectoryProvider.EnvironmentVariableName, null);
            var provider = new MdravaDataDirectoryProvider(Options.Create(new MdravaDataDirectoryOptions()));
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                AssertEx.Equal(Path.Combine(localAppData, "MDRAVA"), provider.GetDataDirectory());
            }
            else
            {
                AssertEx.Equal(Path.Combine(AppContext.BaseDirectory, "MDRAVA"), provider.GetDataDirectory());
            }
        }
        finally
        {
            Environment.SetEnvironmentVariable(MdravaDataDirectoryProvider.EnvironmentVariableName, previous);
        }
    }

    public static async Task LoaderLoadsValidSiteFiles()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded, string.Join("; ", result.Errors));
        var snapshot = AssertEx.NotNull(result.Snapshot);
        AssertEx.Equal(Path.Combine(temp.Path, "config", "sites"), result.SourceDirectory);
        AssertEx.Equal(1, snapshot.Listeners.Count);
        AssertEx.Equal(1, snapshot.Routes.Count);
        AssertEx.Equal("home", snapshot.Routes[0].Name);
        AssertEx.Equal(1, snapshot.SourceFiles.Count);
        AssertEx.Equal(TimeSpan.FromSeconds(10), snapshot.Timeouts.ClientRequestHeadTimeout);
    }

    public static async Task LoaderLoadsRouteLoadBalancingAndHealthCheckSettings()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSiteWithTwoUpstreams(temp.Path, "pool.json", port: 18080, firstUpstreamPort: 15000, secondUpstreamPort: 15001, healthCheckEnabled: true);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded, string.Join("; ", result.Errors));
        var route = AssertEx.NotNull(result.Snapshot).Routes[0];
        AssertEx.Equal("round-robin", route.LoadBalancingPolicy);
        AssertEx.True(route.HealthCheck.Enabled);
        AssertEx.Equal(2, route.Upstreams.Count);
        AssertEx.Equal(2, route.Upstreams[1].Weight);
    }

    public static async Task LoaderCreatesMissingConfigDirectoriesAndLoadsEmptySnapshot()
    {
        using var temp = TemporaryDirectory.Create();
        var configDirectory = Path.Combine(temp.Path, "config");
        var sitesDirectory = Path.Combine(configDirectory, "sites");
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded, string.Join("; ", result.Errors));
        AssertEx.True(Directory.Exists(configDirectory));
        AssertEx.True(Directory.Exists(sitesDirectory));
        var snapshot = AssertEx.NotNull(result.Snapshot);
        AssertEx.Equal(0, snapshot.Listeners.Count);
        AssertEx.Equal(0, snapshot.Routes.Count);
        AssertEx.Equal(0, snapshot.SourceFiles.Count);
    }

    public static async Task LoaderLoadsExistingEmptySitesDirectory()
    {
        using var temp = TemporaryDirectory.Create();
        Directory.CreateDirectory(Path.Combine(temp.Path, "config", "sites"));
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded, string.Join("; ", result.Errors));
        var snapshot = AssertEx.NotNull(result.Snapshot);
        AssertEx.Equal(0, snapshot.Listeners.Count);
        AssertEx.Equal(0, snapshot.Routes.Count);
    }

    public static async Task LoaderUsesDefaultsWhenOperationalConfigIsMissing()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded, string.Join("; ", result.Errors));
        AssertEx.False(File.Exists(Path.Combine(temp.Path, "config", "proxy.json")));
        AssertEx.Equal(TimeSpan.FromSeconds(10), AssertEx.NotNull(result.Snapshot).Timeouts.ClientRequestHeadTimeout);
    }

    public static async Task LoaderLoadsExplicitOperationalTimeouts()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        WriteOperationalConfig(temp.Path, clientRequestHeadTimeoutMs: 250, tunnelIdleTimeoutMs: 750);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded, string.Join("; ", result.Errors));
        AssertEx.Equal(TimeSpan.FromMilliseconds(250), AssertEx.NotNull(result.Snapshot).Timeouts.ClientRequestHeadTimeout);
        AssertEx.Equal(TimeSpan.FromMilliseconds(750), AssertEx.NotNull(result.Snapshot).Timeouts.TunnelIdleTimeout);
    }

    public static async Task LoaderRejectsInvalidOperationalTimeouts()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        WriteOperationalConfig(temp.Path, clientRequestHeadTimeoutMs: 1);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.False(result.Succeeded);
        AssertEx.True(result.Errors.Count > 0);
    }

    public static async Task LoaderRejectsInvalidTunnelLimit()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        WriteOperationalConfig(temp.Path, maxActiveUpgradedTunnels: 0);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.False(result.Succeeded);
        AssertEx.True(result.Errors.Any(static error => error.Contains("MaxActiveUpgradedTunnels", StringComparison.Ordinal)));
    }

    public static async Task LoaderLoadsHttpsListenerWithCertificate()
    {
        using var temp = TemporaryDirectory.Create();
        var certificatePath = Path.Combine(temp.Path, "certs", "home.pfx");
        TestCertificates.WriteSelfSignedPfx(certificatePath, "home.test", "secret");
        WriteHttpsSite(temp.Path, "home.json", port: 18443, upstreamPort: 15000, certificateId: "home-cert");
        WriteOperationalConfig(temp.Path, certificateId: "home-cert", certificatePath: "certs/home.pfx", certificatePassword: "secret");
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded, string.Join("; ", result.Errors));
        var snapshot = AssertEx.NotNull(result.Snapshot);
        AssertEx.Equal(RuntimeListenerTransport.Https, snapshot.Listeners[0].Transport);
        AssertEx.Equal(1, snapshot.Certificates.Count);
        var projection = ProxyConfigurationMapper.ToProjection(snapshot);
        AssertEx.Equal(1, projection.Certificates.Count);
        AssertEx.Equal(true, projection.Certificates[0].HasConfiguredPassword);
    }

    public static async Task LoaderRejectsHttpsListenerWithMissingCertificateReference()
    {
        using var temp = TemporaryDirectory.Create();
        WriteHttpsSite(temp.Path, "home.json", port: 18443, upstreamPort: 15000, certificateId: "missing-cert");
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.False(result.Succeeded);
        AssertEx.True(result.Errors.Any(static error => error.Contains("unknown certificate", StringComparison.OrdinalIgnoreCase)));
    }

    public static async Task LoaderRejectsInvalidCertificatePath()
    {
        using var temp = TemporaryDirectory.Create();
        WriteHttpsSite(temp.Path, "home.json", port: 18443, upstreamPort: 15000, certificateId: "home-cert");
        WriteOperationalConfig(temp.Path, certificateId: "home-cert", certificatePath: "certs/missing.pfx");
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.False(result.Succeeded);
        AssertEx.True(result.Errors.Any(static error => error.Contains("file does not exist", StringComparison.OrdinalIgnoreCase)));
    }

    public static async Task LoaderRejectsInvalidCertificatePassword()
    {
        using var temp = TemporaryDirectory.Create();
        var certificatePath = Path.Combine(temp.Path, "certs", "home.pfx");
        TestCertificates.WriteSelfSignedPfx(certificatePath, "home.test", "correct");
        WriteHttpsSite(temp.Path, "home.json", port: 18443, upstreamPort: 15000, certificateId: "home-cert");
        WriteOperationalConfig(temp.Path, certificateId: "home-cert", certificatePath: "certs/home.pfx", certificatePassword: "wrong");
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.False(result.Succeeded);
        AssertEx.True(result.Errors.Any(static error => error.Contains("could not be loaded", StringComparison.OrdinalIgnoreCase)));
    }

    public static async Task LoaderRejectsDuplicateSniCertificateMapping()
    {
        using var temp = TemporaryDirectory.Create();
        var certificatePath = Path.Combine(temp.Path, "certs", "home.pfx");
        TestCertificates.WriteSelfSignedPfx(certificatePath, "home.test");
        WriteHttpsSite(
            temp.Path,
            "home.json",
            port: 18443,
            upstreamPort: 15000,
            certificateId: "home-cert",
            duplicateSni: true);
        WriteOperationalConfig(temp.Path, certificateId: "home-cert", certificatePath: "certs/home.pfx");
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.False(result.Succeeded);
        AssertEx.True(result.Errors.Any(static error => error.Contains("duplicated", StringComparison.OrdinalIgnoreCase)));
    }

    public static async Task LoaderMergesSniMappingsFromSharedHttpsListener()
    {
        using var temp = TemporaryDirectory.Create();
        var certificatePath = Path.Combine(temp.Path, "certs", "home.pfx");
        TestCertificates.WriteSelfSignedPfx(certificatePath, "home.test");
        WriteHttpsSite(temp.Path, "home.json", port: 18443, upstreamPort: 15000, certificateId: "home-cert", sniHost: "home.test");
        WriteHttpsSite(temp.Path, "alt.json", port: 18443, upstreamPort: 15001, certificateId: "home-cert", sniHost: "alt.test");
        WriteOperationalConfig(temp.Path, certificateId: "home-cert", certificatePath: "certs/home.pfx");
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded, string.Join("; ", result.Errors));
        var snapshot = AssertEx.NotNull(result.Snapshot);
        AssertEx.Equal(1, snapshot.Listeners.Count);
        AssertEx.Equal(2, snapshot.Listeners[0].SniCertificates.Count);
        AssertEx.Equal(2, snapshot.Routes.Count);
    }

    public static async Task LoaderRejectsInvalidSiteFile()
    {
        using var temp = TemporaryDirectory.Create();
        var sites = Directory.CreateDirectory(Path.Combine(temp.Path, "config", "sites")).FullName;
        File.WriteAllText(Path.Combine(sites, "broken.json"), "{ nope");
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.False(result.Succeeded);
        AssertEx.True(result.Errors.Count > 0);
        AssertEx.Equal(null, result.Snapshot);
    }

    public static async Task ReloadPreservesActiveSnapshotWhenLoadFails()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);

        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var first = await service.ReloadAsync(CancellationToken.None);
        AssertEx.True(first.Succeeded);
        AssertEx.Equal(1, store.Snapshot.Version);

        File.WriteAllText(Path.Combine(temp.Path, "config", "sites", "broken.json"), "{ nope");
        var second = await service.ReloadAsync(CancellationToken.None);

        AssertEx.False(second.Succeeded);
        AssertEx.Equal(1, store.Snapshot.Version);
    }

    public static async Task ReloadReplacesActiveSnapshotWhenLoadSucceeds()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);

        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var first = await service.ReloadAsync(CancellationToken.None);
        AssertEx.True(first.Succeeded);

        File.WriteAllText(
            Path.Combine(temp.Path, "config", "sites", "home.json"),
            SiteJson("home", 18081, 15001));

        var second = await service.ReloadAsync(CancellationToken.None);

        AssertEx.True(second.Succeeded, string.Join("; ", second.Errors));
        AssertEx.Equal(2, store.Snapshot.Version);
        AssertEx.Equal(18081, store.Snapshot.Listeners[0].Port);
    }

    public static async Task ReloadReplacesActiveSnapshotWithEmptySitesDirectory()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);

        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var first = await service.ReloadAsync(CancellationToken.None);
        AssertEx.True(first.Succeeded);

        foreach (var siteFile in Directory.EnumerateFiles(Path.Combine(temp.Path, "config", "sites"), "*.json"))
        {
            File.Delete(siteFile);
        }

        var second = await service.ReloadAsync(CancellationToken.None);

        AssertEx.True(second.Succeeded, string.Join("; ", second.Errors));
        AssertEx.Equal(2, store.Snapshot.Version);
        AssertEx.Equal(0, store.Snapshot.Listeners.Count);
        AssertEx.Equal(0, store.Snapshot.Routes.Count);
    }

    public static async Task ActiveInspectionProjectionReflectsStore()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);

        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var result = await service.ReloadAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded);
        var projection = AssertEx.NotNull(result.ActiveConfiguration);
        AssertEx.Equal(1, projection.Version);
        AssertEx.Equal("home", projection.Routes[0].Name);
        AssertEx.Equal(1, projection.SourceFiles.Count);
    }

    internal static void WriteSite(string dataDirectory, string fileName, int port, int upstreamPort)
    {
        var sites = Directory.CreateDirectory(Path.Combine(dataDirectory, "config", "sites")).FullName;
        File.WriteAllText(Path.Combine(sites, fileName), SiteJson(Path.GetFileNameWithoutExtension(fileName), port, upstreamPort));
    }

    internal static void WriteSiteWithTwoUpstreams(
        string dataDirectory,
        string fileName,
        int port,
        int firstUpstreamPort,
        int secondUpstreamPort,
        bool healthCheckEnabled = false,
        int healthIntervalSeconds = 1,
        int healthTimeoutSeconds = 1,
        int healthyThreshold = 1,
        int unhealthyThreshold = 1)
    {
        var sites = Directory.CreateDirectory(Path.Combine(dataDirectory, "config", "sites")).FullName;
        File.WriteAllText(
            Path.Combine(sites, fileName),
            $$"""
            {
              "name": "{{Path.GetFileNameWithoutExtension(fileName)}}",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": {{port}}
                }
              ],
              "host": "*",
              "pathPrefix": "/",
              "loadBalancingPolicy": "round-robin",
              "healthCheck": {
                "enabled": {{healthCheckEnabled.ToString().ToLowerInvariant()}},
                "path": "/health",
                "intervalSeconds": {{healthIntervalSeconds}},
                "timeoutSeconds": {{healthTimeoutSeconds}},
                "healthyThreshold": {{healthyThreshold}},
                "unhealthyThreshold": {{unhealthyThreshold}}
              },
              "upstreams": [
                {
                  "name": "first",
                  "address": "127.0.0.1",
                  "port": {{firstUpstreamPort}},
                  "weight": 1
                },
                {
                  "name": "second",
                  "address": "127.0.0.1",
                  "port": {{secondUpstreamPort}},
                  "weight": 2
                }
              ]
            }
            """);
    }

    internal static void WriteHttpsSite(
        string dataDirectory,
        string fileName,
        int port,
        int upstreamPort,
        string certificateId,
        string host = "*",
        string sniHost = "home.test",
        bool includeDefault = true,
        bool duplicateSni = false)
    {
        var sites = Directory.CreateDirectory(Path.Combine(dataDirectory, "config", "sites")).FullName;
        var defaultCertificateLine = includeDefault
            ? $"""              "defaultCertificateId": "{certificateId}","""
            : "";
        var duplicateSniBlock = duplicateSni
            ? $$"""
                    ,
                    {
                      "hostName": "{{sniHost}}",
                      "certificateId": "{{certificateId}}"
                    }
            """
            : "";

        File.WriteAllText(
            Path.Combine(sites, fileName),
            $$"""
            {
              "name": "{{Path.GetFileNameWithoutExtension(fileName)}}",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": {{port}},
                  "transport": "https",
                  {{defaultCertificateLine}}
                  "sniCertificates": [
                    {
                      "hostName": "{{sniHost}}",
                      "certificateId": "{{certificateId}}"
                    }{{duplicateSniBlock}}
                  ]
                }
              ],
              "host": "{{host}}",
              "pathPrefix": "/",
              "upstreams": [
                {
                  "name": "local-test",
                  "address": "127.0.0.1",
                  "port": {{upstreamPort}}
                }
              ]
            }
            """);
    }

    internal static void WriteOperationalConfig(
        string dataDirectory,
        int clientRequestHeadTimeoutMs = 1000,
        int clientRequestBodyIdleTimeoutMs = 1000,
        int upstreamConnectTimeoutMs = 1000,
        int upstreamResponseHeadTimeoutMs = 1000,
        int upstreamResponseBodyIdleTimeoutMs = 1000,
        int downstreamWriteTimeoutMs = 1000,
        int tlsHandshakeTimeoutMs = 1000,
        int clientKeepAliveIdleTimeoutMs = 1000,
        int upstreamIdleConnectionLifetimeMs = 1000,
        int tunnelIdleTimeoutMs = 1000,
        int maxRequestsPerClientConnection = 100,
        int maxIdleUpstreamConnectionsPerUpstream = 16,
        int maxActiveUpgradedTunnels = 1024,
        string? certificateId = null,
        string? certificatePath = null,
        string? certificatePassword = null,
        string? certificatePasswordEnvironmentVariable = null)
    {
        var configDirectory = Directory.CreateDirectory(Path.Combine(dataDirectory, "config")).FullName;
        var certificatesJson = certificateId is null
            ? "[]"
            : $$"""
            [
                {
                  "id": "{{certificateId}}",
                  "format": "pfx",
                  "path": "{{certificatePath}}"
                  {{(certificatePassword is null ? "" : $""","password": "{certificatePassword}" """)}}
                  {{(certificatePasswordEnvironmentVariable is null ? "" : $""","passwordEnvironmentVariable": "{certificatePasswordEnvironmentVariable}" """)}}
                }
              ]
            """;

        File.WriteAllText(
            Path.Combine(configDirectory, "proxy.json"),
            $$"""
            {
              "timeouts": {
                "clientRequestHeadTimeoutMs": {{clientRequestHeadTimeoutMs}},
                "clientRequestBodyIdleTimeoutMs": {{clientRequestBodyIdleTimeoutMs}},
                "upstreamConnectTimeoutMs": {{upstreamConnectTimeoutMs}},
                "upstreamResponseHeadTimeoutMs": {{upstreamResponseHeadTimeoutMs}},
                "upstreamResponseBodyIdleTimeoutMs": {{upstreamResponseBodyIdleTimeoutMs}},
                "downstreamWriteTimeoutMs": {{downstreamWriteTimeoutMs}},
                "tlsHandshakeTimeoutMs": {{tlsHandshakeTimeoutMs}},
                "clientKeepAliveIdleTimeoutMs": {{clientKeepAliveIdleTimeoutMs}},
                "upstreamIdleConnectionLifetimeMs": {{upstreamIdleConnectionLifetimeMs}},
                "tunnelIdleTimeoutMs": {{tunnelIdleTimeoutMs}}
              },
              "connections": {
                "maxRequestsPerClientConnection": {{maxRequestsPerClientConnection}},
                "maxIdleUpstreamConnectionsPerUpstream": {{maxIdleUpstreamConnectionsPerUpstream}},
                "maxActiveUpgradedTunnels": {{maxActiveUpgradedTunnels}}
              },
              "certificates": {{certificatesJson}}
            }
            """);
    }

    private static ProxyConfigurationLoader CreateLoader(string dataDirectory)
    {
        return new ProxyConfigurationLoader(
            new MdravaDataDirectoryProvider(Options.Create(new MdravaDataDirectoryOptions
            {
                DataDirectory = dataDirectory
            })),
            new MDRAVA.API.Proxy.Configuration.ProxyOptionsValidator(),
            NullLogger<ProxyConfigurationLoader>.Instance);
    }

    private static ProxyConfigurationReloadService CreateReloadService(
        string dataDirectory,
        ProxyConfigurationStore store)
    {
        return new ProxyConfigurationReloadService(
            CreateLoader(dataDirectory),
            store,
            NullLogger<ProxyConfigurationReloadService>.Instance);
    }

    private static string SiteJson(string name, int port, int upstreamPort)
    {
        return $$"""
        {
          "name": "{{name}}",
          "listeners": [
            {
              "name": "main",
              "address": "127.0.0.1",
              "port": {{port}}
            }
          ],
          "host": "*",
          "pathPrefix": "/",
          "upstreams": [
            {
              "name": "local-test",
              "address": "127.0.0.1",
              "port": {{upstreamPort}}
            }
          ]
        }
        """;
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TemporaryDirectory Create()
        {
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mdrava-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return new TemporaryDirectory(path);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}
