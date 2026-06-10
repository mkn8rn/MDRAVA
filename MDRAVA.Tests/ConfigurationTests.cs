using MDRAVA.API.Controllers;
using MDRAVA.INF.Configuration;
using MDRAVA.INF.Configuration.Loading;
using MDRAVA.INF.Configuration.Paths;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace MDRAVA.Tests;

internal static class ConfigurationTests
{
    public static void DataDirectoryUsesConfiguredOverride()
    {
        var expected = Path.Combine(Path.GetTempPath(), $"mdrava-test-{Guid.NewGuid():N}");
        var provider = new MdravaDataDirectoryProvider(new MdravaDataDirectoryOptions
        {
            DataDirectory = expected
        });

        AssertEx.Equal(Path.GetFullPath(expected), provider.GetDataDirectory());
        AssertEx.Equal(Path.Combine(Path.GetFullPath(expected), "config"), provider.GetProxyConfigDirectory());
        AssertEx.Equal(Path.Combine(Path.GetFullPath(expected), "config", "sites"), provider.GetSitesConfigDirectory());
        AssertEx.Equal(Path.Combine(Path.GetFullPath(expected), "logs"), provider.GetLogsDirectory());
        AssertEx.Equal(Path.Combine(Path.GetFullPath(expected), "certs"), provider.GetCertificatesDirectory());
        AssertEx.Equal(Path.Combine(Path.GetFullPath(expected), "state"), provider.GetStateDirectory());
    }

    public static void DataDirectoryUsesEnvironmentOverride()
    {
        var previous = Environment.GetEnvironmentVariable(MdravaDataDirectoryProvider.EnvironmentVariableName);
        var expected = Path.Combine(Path.GetTempPath(), $"mdrava-env-{Guid.NewGuid():N}");

        try
        {
            Environment.SetEnvironmentVariable(MdravaDataDirectoryProvider.EnvironmentVariableName, expected);
            var provider = new MdravaDataDirectoryProvider(new MdravaDataDirectoryOptions
            {
                DataDirectory = Path.Combine(Path.GetTempPath(), "ignored")
            });

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
            var provider = new MdravaDataDirectoryProvider(new MdravaDataDirectoryOptions());
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

    public static async Task LoaderLoadsEquivalentJsonAndYamlSiteFiles()
    {
        using var jsonTemp = TemporaryDirectory.Create();
        using var yamlTemp = TemporaryDirectory.Create();
        WriteSite(jsonTemp.Path, "home.json", port: 18080, upstreamPort: 15000);
        WriteYamlSite(yamlTemp.Path, "home.yml", port: 18080, upstreamPort: 15000);

        var jsonResult = await CreateLoader(jsonTemp.Path).LoadAsync(CancellationToken.None);
        var yamlResult = await CreateLoader(yamlTemp.Path).LoadAsync(CancellationToken.None);

        AssertEx.True(jsonResult.Succeeded, string.Join("; ", jsonResult.Errors));
        AssertEx.True(yamlResult.Succeeded, string.Join("; ", yamlResult.Errors));
        var jsonSnapshot = AssertEx.NotNull(jsonResult.Snapshot);
        var yamlSnapshot = AssertEx.NotNull(yamlResult.Snapshot);
        AssertEx.Equal(jsonSnapshot.Listeners[0].Name, yamlSnapshot.Listeners[0].Name);
        AssertEx.Equal(jsonSnapshot.Listeners[0].Port, yamlSnapshot.Listeners[0].Port);
        AssertEx.Equal(jsonSnapshot.Routes[0].Name, yamlSnapshot.Routes[0].Name);
        AssertEx.Equal(jsonSnapshot.Routes[0].Upstreams[0].Endpoint, yamlSnapshot.Routes[0].Upstreams[0].Endpoint);
        AssertEx.True(yamlSnapshot.Discovery.Files.Any(static file => file.Format == "yaml" && file.Status == "loaded"));
    }

    public static async Task LoaderReportsYamlParseErrorsWithPerFileDiagnostics()
    {
        using var temp = TemporaryDirectory.Create();
        var sites = Directory.CreateDirectory(Path.Combine(temp.Path, "config", "sites")).FullName;
        var yamlPath = Path.Combine(sites, "broken.yaml");
        File.WriteAllText(yamlPath, "name: broken\nlisteners:\n  - name: main\n    port: [not-closed\n");
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.False(result.Succeeded);
        AssertEx.True(result.FileErrors.Any(error => string.Equals(error.Path, yamlPath, StringComparison.OrdinalIgnoreCase)));
        AssertEx.True(result.Errors.Any(static error => error.Contains("YAML", StringComparison.OrdinalIgnoreCase)), string.Join("; ", result.Errors));
        AssertEx.True(result.Discovery.Files.Any(file =>
            string.Equals(file.Path, yamlPath, StringComparison.OrdinalIgnoreCase)
            && file.Format == "yaml"
            && file.Status == "failed"));
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
        AssertEx.True(Directory.Exists(Path.Combine(temp.Path, "logs")));
        AssertEx.True(Directory.Exists(Path.Combine(temp.Path, "certs")));
        AssertEx.True(Directory.Exists(Path.Combine(temp.Path, "state")));
        AssertEx.True(File.Exists(Path.Combine(configDirectory, "proxy.json")));
        AssertEx.True(File.Exists(Path.Combine(sitesDirectory, "example.site.yaml")));
        var snapshot = AssertEx.NotNull(result.Snapshot);
        AssertEx.Equal(0, snapshot.Listeners.Count);
        AssertEx.Equal(0, snapshot.Routes.Count);
        AssertEx.Equal(0, snapshot.SourceFiles.Count);
        AssertEx.True(snapshot.Discovery.CreatedPaths.Count > 0);
        AssertEx.True(snapshot.Discovery.Files.Any(static file => file.Status == "skipped" && file.Format == "yaml"));
    }

    public static async Task LoaderDoesNotOverwriteExistingPlaceholderFiles()
    {
        using var temp = TemporaryDirectory.Create();
        var config = Directory.CreateDirectory(Path.Combine(temp.Path, "config")).FullName;
        var sites = Directory.CreateDirectory(Path.Combine(config, "sites")).FullName;
        var proxyPath = Path.Combine(config, "proxy.json");
        var examplePath = Path.Combine(sites, "example.site.yaml");
        File.WriteAllText(proxyPath, "{ \"observability\": { \"accessLogEnabled\": false } }");
        File.WriteAllText(examplePath, "# custom example");
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded, string.Join("; ", result.Errors));
        AssertEx.Equal("{ \"observability\": { \"accessLogEnabled\": false } }", File.ReadAllText(proxyPath));
        AssertEx.Equal("# custom example", File.ReadAllText(examplePath));
        AssertEx.False(AssertEx.NotNull(result.Snapshot).Observability.AccessLogEnabled);
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
        AssertEx.True(File.Exists(Path.Combine(temp.Path, "config", "proxy.json")));
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

    public static async Task LoaderLoadsObservabilityDefaults()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded, string.Join("; ", result.Errors));
        var observability = AssertEx.NotNull(result.Snapshot).Observability;
        AssertEx.True(observability.AccessLogEnabled);
        AssertEx.Equal(500, observability.RecentDiagnosticsCapacity);
        AssertEx.True(observability.LogPersistence.AccessLogEnabled);
        AssertEx.True(observability.LogPersistence.AdminAuditEnabled);
        AssertEx.Equal(1_048_576L, observability.LogPersistence.MaxFileBytes);
        AssertEx.Equal(8, observability.LogPersistence.MaxFiles);
    }

    public static async Task LoaderLoadsExplicitObservabilitySettings()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        WriteOperationalConfig(
            temp.Path,
            accessLogEnabled: false,
            recentDiagnosticsCapacity: 12,
            accessLogFileEnabled: false,
            adminAuditLogFileEnabled: false,
            logMaxFileBytes: 8192,
            logMaxFiles: 3);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded, string.Join("; ", result.Errors));
        var observability = AssertEx.NotNull(result.Snapshot).Observability;
        AssertEx.False(observability.AccessLogEnabled);
        AssertEx.Equal(12, observability.RecentDiagnosticsCapacity);
        AssertEx.False(observability.LogPersistence.AccessLogEnabled);
        AssertEx.False(observability.LogPersistence.AdminAuditEnabled);
        AssertEx.Equal(8192L, observability.LogPersistence.MaxFileBytes);
        AssertEx.Equal(3, observability.LogPersistence.MaxFiles);
    }

    public static async Task LoaderRejectsInvalidObservabilityCapacity()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        WriteOperationalConfig(temp.Path, recentDiagnosticsCapacity: 0);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.False(result.Succeeded);
        AssertEx.True(result.Errors.Any(static error => error.Contains("RecentDiagnosticsCapacity", StringComparison.Ordinal)));
    }

    public static async Task LoaderRejectsInvalidLogPersistenceSettings()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        WriteOperationalConfig(temp.Path, logMaxFileBytes: 1024, logMaxFiles: 0);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.False(result.Succeeded);
        AssertEx.True(result.Errors.Any(static error => error.Contains("MaxFileBytes", StringComparison.Ordinal)), string.Join("; ", result.Errors));
        AssertEx.True(result.Errors.Any(static error => error.Contains("MaxFiles", StringComparison.Ordinal)), string.Join("; ", result.Errors));
    }

    public static async Task LoaderLoadsLimitDefaults()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded, string.Join("; ", result.Errors));
        var limits = AssertEx.NotNull(result.Snapshot).Limits;
        AssertEx.Equal(4096, limits.MaxActiveClientConnections);
        AssertEx.Equal(128, limits.MaxConcurrentTlsHandshakes);
        AssertEx.Equal(240, limits.RequestsPerMinutePerIp);
        AssertEx.Equal(TimeSpan.FromSeconds(15), limits.ShutdownGracePeriod);
    }

    public static async Task LoaderRejectsInvalidLimitSettings()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        WriteOperationalConfig(temp.Path, maxActiveClientConnections: 0);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.False(result.Succeeded);
        AssertEx.True(result.Errors.Any(static error => error.Contains("MaxActiveClientConnections", StringComparison.Ordinal)));
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
        var projection = ProxyConfigurationProjectionMapper.ToProjection(
            snapshot,
            TestHttp3PlatformSupport.Supported);
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

    public static void ConfigReadOperationsProjectActiveAndEffectiveFromCurrentSource()
    {
        var projection = new TestConfigurationProjection("current");
        var operations = new ProxyConfigurationReadOperations<TestConfigurationProjection>(
            new FixedConfigurationReadProjectionSource<TestConfigurationProjection>(projection));
        var missingOperations = new ProxyConfigurationReadOperations<TestConfigurationProjection>(
            new FixedConfigurationReadProjectionSource<TestConfigurationProjection>(null));

        var active = operations.ReadActive();
        var effective = operations.ReadEffective();
        var missingActive = missingOperations.ReadActive();
        var missingEffective = missingOperations.ReadEffective();

        AssertEx.True(active.Found);
        AssertEx.Equal(projection, AssertEx.NotNull(active.Configuration));
        AssertEx.True(effective.Found);
        AssertEx.Equal(projection, AssertEx.NotNull(effective.Configuration));
        AssertEx.False(missingActive.Found);
        AssertEx.Equal(null, missingActive.Configuration);
        AssertEx.False(missingEffective.Found);
        AssertEx.Equal(null, missingEffective.Configuration);
    }

    public static async Task LoaderRejectsUnsafeHeaderRule()
    {
        using var temp = TemporaryDirectory.Create();
        WriteCustomSite(
            temp.Path,
            "unsafe.json",
            """
            {
              "name": "unsafe",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": 18080
                }
              ],
              "host": "*",
              "routes": [
                {
                  "name": "app",
                  "pathPrefix": "/",
                  "action": "proxy",
                  "headerPolicy": {
                    "setRequestHeaders": [
                      {
                        "name": "Content-Length",
                        "value": "1"
                      }
                    ]
                  },
                  "upstreams": [
                    {
                      "name": "local",
                      "address": "127.0.0.1",
                      "port": 15000
                    }
                  ]
                }
              ]
            }
            """);
        var loader = CreateLoader(temp.Path);

        var result = await loader.LoadAsync(CancellationToken.None);

        AssertEx.False(result.Succeeded);
        AssertEx.True(result.Errors.Any(static error => error.Contains("restricted", StringComparison.OrdinalIgnoreCase)), string.Join("; ", result.Errors));
    }

    public static async Task ResponseHeaderPolicyCannotEmitHopByHopHeaders()
    {
        using var temp = TemporaryDirectory.Create();
        WriteCustomSite(
            temp.Path,
            "unsafe-response.json",
            """
            {
              "name": "unsafe-response",
              "listeners": [
                {
                  "name": "main",
                  "address": "127.0.0.1",
                  "port": 18080
                }
              ],
              "host": "*",
              "routes": [
                {
                  "name": "app",
                  "pathPrefix": "/",
                  "action": "proxy",
                  "headerPolicy": {
                    "setResponseHeaders": [
                      {
                        "name": "Connection",
                        "value": "keep-alive"
                      }
                    ]
                  },
                  "upstreams": [
                    {
                      "name": "local",
                      "address": "127.0.0.1",
                      "port": 15000
                    }
                  ]
                }
              ]
            }
            """);

        var result = await CreateLoader(temp.Path).LoadAsync(CancellationToken.None);

        AssertEx.False(result.Succeeded);
        AssertEx.True(result.Errors.Any(static error => error.Contains("restricted", StringComparison.OrdinalIgnoreCase)), string.Join("; ", result.Errors));
    }

    public static async Task MultiFileConfigConflictReportingIsDeterministic()
    {
        using var temp = TemporaryDirectory.Create();
        TestCertificates.WriteSelfSignedPfx(Path.Combine(temp.Path, "certs", "home.pfx"), "home.test");
        TestCertificates.WriteSelfSignedPfx(Path.Combine(temp.Path, "certs", "alt.pfx"), "alt.test");
        File.WriteAllText(
            Path.Combine(Directory.CreateDirectory(Path.Combine(temp.Path, "config")).FullName, "proxy.json"),
            """
            {
              "certificates": [
                {
                  "id": "home-cert",
                  "format": "pfx",
                  "path": "certs/home.pfx"
                },
                {
                  "id": "alt-cert",
                  "format": "pfx",
                  "path": "certs/alt.pfx"
                }
              ]
            }
            """);
        WriteHttpsSite(temp.Path, "home-a.json", port: 18443, upstreamPort: 15000, certificateId: "home-cert");
        WriteHttpsSite(temp.Path, "home-b.json", port: 18443, upstreamPort: 15001, certificateId: "alt-cert");

        var first = await CreateLoader(temp.Path).LoadAsync(CancellationToken.None);
        var second = await CreateLoader(temp.Path).LoadAsync(CancellationToken.None);

        AssertEx.False(first.Succeeded);
        AssertEx.False(second.Succeeded);
        AssertEx.Equal(string.Join("\n", first.Errors), string.Join("\n", second.Errors));
        AssertEx.True(first.Errors.Any(static error => error.Contains("default certificate", StringComparison.OrdinalIgnoreCase)), string.Join("; ", first.Errors));
    }

    public static async Task ConfigValidateReportsValidWithoutApplying()
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

        var validation = await service.ValidateAsync(CancellationToken.None);

        AssertEx.True(validation.Succeeded, string.Join("; ", validation.Errors));
        AssertEx.Equal(1, store.Snapshot.Version);
        AssertEx.Equal(2, validation.WouldBeVersion);
        AssertEx.Equal(18080, store.Snapshot.Listeners[0].Port);
    }

    public static async Task ConfigValidateReportsInvalidWithoutReplacingActiveConfig()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var first = await service.ReloadAsync(CancellationToken.None);
        AssertEx.True(first.Succeeded);

        File.WriteAllText(Path.Combine(temp.Path, "config", "sites", "broken.json"), "{ nope");
        var validation = await service.ValidateAsync(CancellationToken.None);

        AssertEx.False(validation.Succeeded);
        AssertEx.Equal(1, store.Snapshot.Version);
        AssertEx.True(validation.FileErrors.Any(error => error.Path?.EndsWith("broken.json", StringComparison.OrdinalIgnoreCase) == true));
    }

    public static async Task ConfigNormalizeConvertsYamlToJsonWithoutApplying()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var first = await service.ReloadAsync(CancellationToken.None);
        AssertEx.True(first.Succeeded);
        var normalizer = CreateNormalizer();
        var reloadAdministration = new ProxyConfigurationReloadAdministrationService<ProxyConfigurationProjection>(
            service);
        var controller = new ProxyConfigurationController(
            new ProxyConfigurationAdministrationService(normalizer, service),
            CreateReadAdministration(store),
            reloadAdministration);

        var actionResult = controller.Normalize(new ProxyConfigurationNormalizeRequest(
            "yaml",
            YamlSiteText("normalized", port: 18081, upstreamPort: 15001)));

        var ok = (OkObjectResult)AssertEx.NotNull(actionResult.Result);
        var normalize = (ProxyConfigurationNormalizeResult)AssertEx.NotNull(ok.Value);
        AssertEx.True(normalize.Succeeded, string.Join("; ", normalize.Errors));
        AssertEx.True(AssertEx.NotNull(normalize.CanonicalJson).Contains("\"Name\": \"normalized\"", StringComparison.Ordinal));
        AssertEx.Equal(1, store.Snapshot.Version);
        AssertEx.Equal(18080, store.Snapshot.Listeners[0].Port);
    }

    public static void ConfigNormalizerShapesValidationFailuresFromParsedSite()
    {
        var parser = new FixedNormalizeSiteParser(
            new ProxyConfigurationNormalizeSiteParseResult(
                new SiteOptions
                {
                    Name = "broken",
                    Host = "*",
                    PathPrefix = "/"
                },
                "{}",
                null));
        var normalizer = new ProxyConfigurationNormalizer(parser);

        var result = normalizer.Normalize(new ProxyConfigurationNormalizeRequest("yml", "ignored"));

        AssertEx.False(result.Succeeded);
        AssertEx.Equal(ProxyConfigurationNormalizeFormat.Yaml, parser.LastFormat);
        AssertEx.Equal("yaml", result.Format);
        AssertEx.Equal(null, result.CanonicalJson);
        AssertEx.True(result.Errors.Any(static error => error.Contains("Proxy:Listeners", StringComparison.Ordinal)), string.Join("; ", result.Errors));
        AssertEx.True(result.FileErrors.All(static error => error.Path is null));
    }

    public static async Task EffectiveConfigProjectionRedactsCertificateSecrets()
    {
        using var temp = TemporaryDirectory.Create();
        var certificatePath = Path.Combine(temp.Path, "certs", "home.pfx");
        TestCertificates.WriteSelfSignedPfx(certificatePath, "home.test", "secret");
        WriteHttpsSite(temp.Path, "home.json", port: 18443, upstreamPort: 15000, certificateId: "home-cert");
        WriteOperationalConfig(temp.Path, certificateId: "home-cert", certificatePath: "certs/home.pfx", certificatePassword: "secret");
        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var result = await service.ReloadAsync(CancellationToken.None);
        AssertEx.True(result.Succeeded, string.Join("; ", result.Errors));

        var reloadAdministration = new ProxyConfigurationReloadAdministrationService<ProxyConfigurationProjection>(
            service);
        var controller = new ProxyConfigurationController(
            new ProxyConfigurationAdministrationService(
                CreateNormalizer(),
                service),
            CreateReadAdministration(store),
            reloadAdministration);
        var actionResult = controller.Effective();
        var ok = (OkObjectResult)AssertEx.NotNull(actionResult.Result);
        var projection = (ProxyConfigurationProjection)AssertEx.NotNull(ok.Value);

        AssertEx.Equal(true, projection.Certificates[0].HasConfiguredPassword);
        AssertEx.False(projection.ToString().Contains("secret", StringComparison.OrdinalIgnoreCase));
    }

    public static async Task ExpiredCertificateProjectionKeepsValidityWindowVisible()
    {
        using var temp = TemporaryDirectory.Create();
        var notBefore = DateTimeOffset.UtcNow.AddDays(-10);
        var notAfter = DateTimeOffset.UtcNow.AddDays(-1);
        var certificatePath = Path.Combine(temp.Path, "certs", "expired.pfx");
        Directory.CreateDirectory(Path.GetDirectoryName(certificatePath)!);
        File.WriteAllBytes(
            certificatePath,
            TestCertificates.CreateSelfSignedPfxBytesForValidity("expired.test", null, notBefore, notAfter));
        WriteHttpsSite(temp.Path, "expired.json", port: 18443, upstreamPort: 15000, certificateId: "expired-cert");
        WriteOperationalConfig(temp.Path, certificateId: "expired-cert", certificatePath: "certs/expired.pfx");

        var result = await CreateLoader(temp.Path).LoadAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded, string.Join("; ", result.Errors));
        var projection = ProxyConfigurationProjectionMapper.ToProjection(
            AssertEx.NotNull(result.Snapshot),
            TestHttp3PlatformSupport.Supported);
        AssertEx.True(projection.Certificates[0].NotAfter < DateTime.UtcNow);
        AssertEx.True(projection.Certificates[0].NotBefore < projection.Certificates[0].NotAfter);
    }

    public static async Task NotYetValidCertificateProjectionKeepsValidityWindowVisible()
    {
        using var temp = TemporaryDirectory.Create();
        var notBefore = DateTimeOffset.UtcNow.AddDays(2);
        var notAfter = DateTimeOffset.UtcNow.AddDays(30);
        var certificatePath = Path.Combine(temp.Path, "certs", "future.pfx");
        Directory.CreateDirectory(Path.GetDirectoryName(certificatePath)!);
        File.WriteAllBytes(
            certificatePath,
            TestCertificates.CreateSelfSignedPfxBytesForValidity("future.test", null, notBefore, notAfter));
        WriteHttpsSite(temp.Path, "future.json", port: 18443, upstreamPort: 15000, certificateId: "future-cert");
        WriteOperationalConfig(temp.Path, certificateId: "future-cert", certificatePath: "certs/future.pfx");

        var result = await CreateLoader(temp.Path).LoadAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded, string.Join("; ", result.Errors));
        var projection = ProxyConfigurationProjectionMapper.ToProjection(
            AssertEx.NotNull(result.Snapshot),
            TestHttp3PlatformSupport.Supported);
        AssertEx.True(projection.Certificates[0].NotBefore > DateTime.UtcNow);
        AssertEx.True(projection.Certificates[0].NotAfter > projection.Certificates[0].NotBefore);
    }

    public static async Task ReloadFailureReportsPerFileErrorAndPreservesActiveConfig()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var first = await service.ReloadAsync(CancellationToken.None);
        AssertEx.True(first.Succeeded);
        var loadedAt = store.Snapshot.LoadedAtUtc;

        File.WriteAllText(Path.Combine(temp.Path, "config", "sites", "broken.json"), "{ nope");
        var second = await service.ReloadAsync(CancellationToken.None);

        AssertEx.False(second.Succeeded);
        AssertEx.Equal(1, store.Snapshot.Version);
        AssertEx.Equal(1, second.ActiveVersion);
        AssertEx.Equal(loadedAt, second.LastSuccessfulLoadAtUtc);
        AssertEx.True(second.FileErrors.Any(error => error.Path?.EndsWith("broken.json", StringComparison.OrdinalIgnoreCase) == true));
    }

    public static async Task ReloadWithInvalidLogPersistenceConfigPreservesActiveSnapshot()
    {
        using var temp = TemporaryDirectory.Create();
        WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        WriteOperationalConfig(temp.Path, logMaxFileBytes: 8192, logMaxFiles: 2);
        var store = new ProxyConfigurationStore();
        var service = CreateReloadService(temp.Path, store);
        var first = await service.ReloadAsync(CancellationToken.None);
        AssertEx.True(first.Succeeded);

        WriteOperationalConfig(temp.Path, logMaxFileBytes: 1024, logMaxFiles: 0);
        var second = await service.ReloadAsync(CancellationToken.None);

        AssertEx.False(second.Succeeded);
        AssertEx.Equal(1, store.Snapshot.Version);
        AssertEx.Equal(8192L, store.Snapshot.Observability.LogPersistence.MaxFileBytes);
        AssertEx.Equal(2, store.Snapshot.Observability.LogPersistence.MaxFiles);
        AssertEx.True(second.Errors.Any(static error => error.Contains("MaxFileBytes", StringComparison.Ordinal)), string.Join("; ", second.Errors));
        AssertEx.True(second.Errors.Any(static error => error.Contains("MaxFiles", StringComparison.Ordinal)), string.Join("; ", second.Errors));
    }

    internal static void WriteSite(string dataDirectory, string fileName, int port, int upstreamPort)
    {
        var sites = Directory.CreateDirectory(Path.Combine(dataDirectory, "config", "sites")).FullName;
        File.WriteAllText(Path.Combine(sites, fileName), SiteJson(Path.GetFileNameWithoutExtension(fileName), port, upstreamPort));
    }

    internal static void WriteCustomSite(string dataDirectory, string fileName, string json)
    {
        var sites = Directory.CreateDirectory(Path.Combine(dataDirectory, "config", "sites")).FullName;
        File.WriteAllText(Path.Combine(sites, fileName), json);
    }

    internal static void WriteYamlSite(string dataDirectory, string fileName, int port, int upstreamPort)
    {
        var sites = Directory.CreateDirectory(Path.Combine(dataDirectory, "config", "sites")).FullName;
        File.WriteAllText(Path.Combine(sites, fileName), YamlSiteText(Path.GetFileNameWithoutExtension(fileName), port, upstreamPort));
    }

    private static string YamlSiteText(string name, int port, int upstreamPort)
    {
        return $$"""
        name: {{name}}
        listeners:
          - name: main
            address: 127.0.0.1
            port: {{port}}
        host: "*"
        pathPrefix: /
        upstreams:
          - name: local-test
            address: 127.0.0.1
            port: {{upstreamPort}}
        """;
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
        bool accessLogEnabled = true,
        int recentDiagnosticsCapacity = 500,
        bool accessLogFileEnabled = true,
        bool adminAuditLogFileEnabled = true,
        long logMaxFileBytes = 1_048_576,
        int logMaxFiles = 8,
        int maxActiveClientConnections = 4096,
        int maxConcurrentTlsHandshakes = 128,
        int requestsPerMinutePerIp = 240,
        int upgradeRequestsPerMinutePerIp = 30,
        int maxRequestHeadBytes = 32768,
        int maxHeaderCount = 128,
        int maxHeaderLineBytes = 8192,
        long maxRequestBodyBytes = 104857600,
        int maxPathBytes = 8192,
        int shutdownGracePeriodSeconds = 15,
        bool forwardedHeadersEnabled = true,
        string[]? trustedProxies = null,
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
        var trustedProxiesJson = trustedProxies is null
            ? "[]"
            : "[" + string.Join(", ", trustedProxies.Select(static proxy => $@"""{proxy}""")) + "]";

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
              "observability": {
                "accessLogEnabled": {{accessLogEnabled.ToString().ToLowerInvariant()}},
                "recentDiagnosticsCapacity": {{recentDiagnosticsCapacity}},
                "logPersistence": {
                  "accessLogEnabled": {{accessLogFileEnabled.ToString().ToLowerInvariant()}},
                  "adminAuditEnabled": {{adminAuditLogFileEnabled.ToString().ToLowerInvariant()}},
                  "maxFileBytes": {{logMaxFileBytes}},
                  "maxFiles": {{logMaxFiles}}
                }
              },
              "limits": {
                "maxActiveClientConnections": {{maxActiveClientConnections}},
                "maxConcurrentTlsHandshakes": {{maxConcurrentTlsHandshakes}},
                "requestsPerMinutePerIp": {{requestsPerMinutePerIp}},
                "upgradeRequestsPerMinutePerIp": {{upgradeRequestsPerMinutePerIp}},
                "maxRequestHeadBytes": {{maxRequestHeadBytes}},
                "maxHeaderCount": {{maxHeaderCount}},
                "maxHeaderLineBytes": {{maxHeaderLineBytes}},
                "maxRequestBodyBytes": {{maxRequestBodyBytes}},
                "maxPathBytes": {{maxPathBytes}},
                "shutdownGracePeriodSeconds": {{shutdownGracePeriodSeconds}}
              },
              "forwardedHeaders": {
                "enabled": {{forwardedHeadersEnabled.ToString().ToLowerInvariant()}},
                "trustedProxies": {{trustedProxiesJson}}
              },
              "certificates": {{certificatesJson}}
            }
            """);
    }

    private static ProxyConfigurationLoader CreateLoader(string dataDirectory)
    {
        return new ProxyConfigurationLoader(
            new MdravaDataDirectoryProvider(new MdravaDataDirectoryOptions
            {
                DataDirectory = dataDirectory
            }),
            new ProxyDataDirectoryBootstrapper(new MdravaDataDirectoryProvider(new MdravaDataDirectoryOptions
            {
                DataDirectory = dataDirectory
            })),
            new SiteConfigurationParser(),
            new ProxyRelativeStoragePathPolicy(),
            NullLogger<ProxyConfigurationLoader>.Instance);
    }

    private static ProxyConfigurationReloadService CreateReloadService(
        string dataDirectory,
        ProxyConfigurationStore store)
    {
        return new ProxyConfigurationReloadService(
            CreateLoader(dataDirectory),
            store,
            new ResponseCacheStore(TimeProvider.System),
            new ProxyMetrics(),
            ActivatingProxyListenerReloadApplier.Instance,
            SilentProxyConfigurationReloadEventSink.Instance,
            TestHttp3PlatformSupport.SupportedSource);
    }

    private static ProxyConfigurationReadAdministrationService<ProxyConfigurationProjection> CreateReadAdministration(
        IProxyConfigurationStore store)
    {
        return new ProxyConfigurationReadAdministrationService<ProxyConfigurationProjection>(
            new ProxyConfigurationReadOperations<ProxyConfigurationProjection>(
                new ProxyConfigurationReadProjectionSource(
                    store,
                    TestHttp3PlatformSupport.SupportedSource)));
    }

    private static ProxyConfigurationNormalizer CreateNormalizer()
    {
        return new ProxyConfigurationNormalizer(
            new ProxyConfigurationNormalizeSiteParser(new SiteConfigurationParser()));
    }

    private sealed record TestConfigurationProjection(string Name);

    private sealed class FixedNormalizeSiteParser : IProxyConfigurationNormalizeSiteParser
    {
        private readonly ProxyConfigurationNormalizeSiteParseResult _result;

        public FixedNormalizeSiteParser(ProxyConfigurationNormalizeSiteParseResult result)
        {
            _result = result;
        }

        public ProxyConfigurationNormalizeFormat? LastFormat { get; private set; }

        public ProxyConfigurationNormalizeSiteParseResult Parse(
            string text,
            ProxyConfigurationNormalizeFormat format)
        {
            _ = text;
            LastFormat = format;
            return _result;
        }
    }

    private sealed class FixedConfigurationReadProjectionSource<TConfiguration>
        : IProxyConfigurationReadProjectionSource<TConfiguration>
        where TConfiguration : class
    {
        private readonly TConfiguration? _projection;

        public FixedConfigurationReadProjectionSource(TConfiguration? projection)
        {
            _projection = projection;
        }

        public TConfiguration? ReadCurrent()
        {
            return _projection;
        }
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
