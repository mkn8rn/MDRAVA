using System.Text.Json;
using MDRAVA.INF.Configuration.Loading;
using MDRAVA.INF.Configuration.Paths;
using MDRAVA.INF.DataDirectory;
using Microsoft.Extensions.Logging.Abstractions;

namespace MDRAVA.Tests;

internal static class BackupRestoreTests
{
    public static void BackupManifestClassifiesDataDirectoryWithoutFileContentsOrSecrets()
    {
        const string secret = "phase-51-secret-value";
        using var temp = TemporaryDirectory.Create();
        Directory.CreateDirectory(Path.Combine(temp.Path, "config", "sites"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "logs"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "certs", "acme", "private-keys", "home"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "certs", "acme", "metadata", "home"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "state"));
        File.WriteAllText(Path.Combine(temp.Path, "config", "proxy.json"), $"{{ \"token\": \"{secret}\" }}");
        File.WriteAllText(Path.Combine(temp.Path, "config", "sites", "home.json"), "{ \"name\": \"home\" }");
        File.WriteAllText(Path.Combine(temp.Path, "logs", "access.log"), $"Authorization: Bearer {secret}");
        File.WriteAllText(Path.Combine(temp.Path, "certs", "manual.pfx"), secret);
        File.WriteAllText(Path.Combine(temp.Path, "certs", "acme", "private-keys", "home", "current.pfx"), secret);
        File.WriteAllText(Path.Combine(temp.Path, "certs", "acme", "metadata", "home", "status.json"), "{ \"id\": \"home\" }");
        File.WriteAllText(Path.Combine(temp.Path, "state", "runtime.json"), secret);

        var manifest = CreateService(temp.Path).CreateManifest();
        var text = JsonSerializer.Serialize(manifest);

        AssertEx.True(manifest.Entries.All(static entry => !Path.IsPathRooted(entry.RelativePath)));
        AssertEx.True(manifest.Entries.All(static entry => !entry.RelativePath.StartsWith("..", StringComparison.Ordinal)));
        AssertEx.True(manifest.Entries.Any(static entry =>
            entry.RelativePath == "config/proxy.json"
            && entry.Category == "config"
            && entry.Classification == "must_backup"
            && !entry.Sensitive));
        AssertEx.True(manifest.Entries.Any(static entry =>
            entry.RelativePath == "logs/access.log"
            && entry.Category == "logs"
            && entry.Classification == "should_backup"));
        AssertEx.True(manifest.Entries.Any(static entry =>
            entry.RelativePath == "certs/manual.pfx"
            && entry.Category == "manual_certificate_material"
            && entry.Classification == "never_export_by_default_sensitive"
            && entry.Sensitive));
        AssertEx.True(manifest.Entries.Any(static entry =>
            entry.RelativePath == "certs/acme/private-keys/home/current.pfx"
            && entry.Category == "acme_secret_material"
            && entry.Sensitive));
        AssertEx.True(manifest.Entries.Any(static entry =>
            entry.RelativePath == "state/runtime.json"
            && entry.Category == "state"));
        AssertEx.False(text.Contains(secret, StringComparison.Ordinal), text);
        AssertEx.False(text.Contains("Authorization", StringComparison.OrdinalIgnoreCase), text);
    }

    public static void BackupManifestReportsMissingDirectoriesWithBoundedWarnings()
    {
        using var temp = TemporaryDirectory.Create();

        var manifest = CreateService(temp.Path).CreateManifest();

        AssertEx.True(manifest.Directories.Any(static directory => directory.RelativePath == "config" && !directory.Exists));
        AssertEx.True(manifest.Directories.Any(static directory => directory.RelativePath == "logs" && !directory.Exists));
        AssertEx.True(manifest.Warnings.Count <= 64);
        AssertEx.True(manifest.Warnings.Any(static warning => warning.Code == "missing_directory"));
        AssertEx.True(manifest.Warnings.All(static warning => warning.RelativePath is null || !Path.IsPathRooted(warning.RelativePath)));
    }

    public static void BackupPathSafetyRejectsTraversalOutsideDataDirectory()
    {
        using var temp = TemporaryDirectory.Create();
        var inside = Path.Combine(temp.Path, "config", "proxy.json");
        var outside = Path.Combine(temp.Path, "..", "outside.json");

        AssertEx.True(ProxyBackupPathSafety.TryGetSafeRelativePath(temp.Path, inside, out var relative));
        AssertEx.Equal("config/proxy.json", relative);
        AssertEx.False(ProxyBackupPathSafety.TryGetSafeRelativePath(temp.Path, outside, out _));
    }

    public static async Task RestoreValidationCatchesInvalidConfigWithoutCreatingBootstrapFiles()
    {
        using var temp = TemporaryDirectory.Create();
        Directory.CreateDirectory(Path.Combine(temp.Path, "config", "sites"));
        File.WriteAllText(Path.Combine(temp.Path, "config", "sites", "broken.json"), "{ nope");

        var result = await CreateService(temp.Path).ValidateAsync(CancellationToken.None);

        AssertEx.False(result.Succeeded);
        AssertEx.True(result.Errors.Any(static error => error.Code == "config_parse_failed"), string.Join(",", result.Errors.Select(static error => error.Code)));
        AssertEx.False(File.Exists(Path.Combine(temp.Path, "config", "proxy.json")));
        AssertEx.False(File.Exists(Path.Combine(temp.Path, "config", "sites", "example.site.yaml")));
    }

    public static async Task RestoreValidationCatchesMissingReferencedCertificateMaterial()
    {
        using var temp = TemporaryDirectory.Create();
        ConfigurationTests.WriteHttpsSite(temp.Path, "home.json", port: 18443, upstreamPort: 15000, certificateId: "home-cert");
        ConfigurationTests.WriteOperationalConfig(temp.Path, certificateId: "home-cert", certificatePath: "certs/missing.pfx");

        var result = await CreateService(temp.Path).ValidateAsync(CancellationToken.None);

        AssertEx.False(result.Succeeded);
        AssertEx.True(result.Errors.Any(static error => error.Code == "certificate_file_missing"), string.Join(",", result.Errors.Select(static error => error.Code)));
        AssertEx.False(JsonSerializer.Serialize(result).Contains("missing.pfx", StringComparison.OrdinalIgnoreCase));
    }

    public static async Task RestoreValidationPreservesExistingActiveRuntimeState()
    {
        using var temp = TemporaryDirectory.Create();
        ConfigurationTests.WriteSite(temp.Path, "home.json", port: 18080, upstreamPort: 15000);
        var store = new ProxyConfigurationStore();
        var loader = CreateLoader(temp.Path);
        var load = await loader.LoadAsync(CancellationToken.None);
        store.Replace(AssertEx.NotNull(load.Snapshot));
        var loadedAt = store.Snapshot.LoadedAtUtc;
        File.WriteAllText(Path.Combine(temp.Path, "config", "sites", "broken.json"), "{ nope");

        var result = await CreateService(temp.Path, store, loader).ValidateAsync(CancellationToken.None);

        AssertEx.False(result.Succeeded);
        AssertEx.Equal(1, result.ActiveConfigVersion);
        AssertEx.Equal(1, store.Snapshot.Version);
        AssertEx.Equal(loadedAt, store.Snapshot.LoadedAtUtc);
    }

    public static async Task RestoreValidationSucceedsWithBootstrapLayout()
    {
        using var temp = TemporaryDirectory.Create();
        var loader = CreateLoader(temp.Path);
        var load = await loader.LoadAsync(CancellationToken.None);
        AssertEx.True(load.Succeeded, string.Join(";", load.Errors));

        var result = await CreateService(temp.Path, loader: loader).ValidateAsync(CancellationToken.None);

        AssertEx.True(result.Succeeded, string.Join(",", result.Errors.Select(static error => error.Code)));
        AssertEx.True(result.Manifest.Directories
            .Where(static directory => directory.RelativePath != "certs/acme")
            .All(static directory => directory.Exists));
        AssertEx.True(File.Exists(Path.Combine(temp.Path, "config", "proxy.json")));
        AssertEx.True(File.Exists(Path.Combine(temp.Path, "config", "sites", "example.site.yaml")));
    }

    private static ProxyBackupService CreateService(
        string dataDirectory,
        ProxyConfigurationStore? store = null,
        ProxyConfigurationLoader? loader = null)
    {
        return new ProxyBackupService(
            Provider(dataDirectory),
            new ProxyBackupFileSystem(),
            loader ?? CreateLoader(dataDirectory),
            store ?? new ProxyConfigurationStore());
    }

    private static ProxyConfigurationLoader CreateLoader(string dataDirectory)
    {
        var provider = Provider(dataDirectory);
        return new ProxyConfigurationLoader(
            provider,
            new ProxyDataDirectoryBootstrapper(provider),
            new SiteConfigurationParser(),
            NullLogger<ProxyConfigurationLoader>.Instance);
    }

    private static MdravaDataDirectoryProvider Provider(string dataDirectory)
    {
        return new MdravaDataDirectoryProvider(new MdravaDataDirectoryOptions
        {
            DataDirectory = dataDirectory
        });
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
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mdrava-backup-tests-{Guid.NewGuid():N}");
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
