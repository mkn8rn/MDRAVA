using System.Text.Json;
using MDRAVA.INF.Configuration.Paths;

namespace MDRAVA.Tests;

internal static class RuntimePreflightTests
{
    public static void RuntimePreflightCreatesOwnedDirectoriesSafely()
    {
        using var temp = TemporaryDirectory.Create();
        var generatedAtUtc = new DateTimeOffset(2026, 6, 10, 7, 45, 0, TimeSpan.Zero);
        var service = new ProxyRuntimePreflightService(
            Provider(temp.Path),
            new ProxyDataDirectoryPathSafety(),
            new ProxyRuntimeDirectoryProbe(),
            new FixedTimeProvider(generatedAtUtc));

        var status = service.RunStartupChecks();

        AssertEx.Equal("healthy", status.State);
        AssertEx.Equal(generatedAtUtc, status.GeneratedAtUtc);
        AssertEx.True(Directory.Exists(Path.Combine(temp.Path, "config")));
        AssertEx.True(Directory.Exists(Path.Combine(temp.Path, "config", "sites")));
        AssertEx.True(Directory.Exists(Path.Combine(temp.Path, "logs")));
        AssertEx.True(Directory.Exists(Path.Combine(temp.Path, "certs")));
        AssertEx.True(Directory.Exists(Path.Combine(temp.Path, "state")));
        AssertEx.True(status.Checks.All(static check => !Path.IsPathRooted(check.RelativePath)));
        AssertEx.True(status.Checks.All(static check => check.Reason == "ok"));
    }

    public static void RuntimePreflightReportsUnwritableDirectorySafely()
    {
        const string secret = "phase-52-directory-secret";
        using var temp = TemporaryDirectory.Create();
        var probe = new DelegateProbe(path =>
            path.EndsWith("logs", StringComparison.OrdinalIgnoreCase)
                ? new ProxyRuntimeDirectoryProbeResult(true, false, true, false, secret)
                : new ProxyRuntimeDirectoryProbeResult(true, false, true, true, null));
        var service = new ProxyRuntimePreflightService(
            Provider(temp.Path),
            new ProxyDataDirectoryPathSafety(),
            probe,
            TimeProvider.System);

        var status = service.RunStartupChecks();
        var text = JsonSerializer.Serialize(status);

        AssertEx.Equal("degraded", status.State);
        AssertEx.True(status.Reasons.Contains("directory_not_writable"), string.Join(",", status.Reasons));
        AssertEx.True(status.Checks.Any(static check =>
            check.Name == "logs_directory"
            && check.Severity == "warning"
            && check.Reason == "directory_not_writable"));
        AssertEx.False(text.Contains(secret, StringComparison.Ordinal), text);
        AssertEx.False(text.Contains(temp.Path, StringComparison.OrdinalIgnoreCase), text);
        AssertEx.False(text.Contains("Authorization", StringComparison.OrdinalIgnoreCase), text);
        AssertEx.False(text.Contains("Cookie", StringComparison.OrdinalIgnoreCase), text);
    }

    public static void RuntimePreflightRejectsUnsafeChildPath()
    {
        using var temp = TemporaryDirectory.Create();
        var provider = new UnsafeLogsProvider(temp.Path, Path.Combine(Path.GetTempPath(), $"mdrava-outside-{Guid.NewGuid():N}"));
        var service = new ProxyRuntimePreflightService(
            provider,
            new ProxyDataDirectoryPathSafety(),
            new DelegateProbe(_ => new ProxyRuntimeDirectoryProbeResult(true, false, true, true, null)),
            TimeProvider.System);

        var status = service.RunStartupChecks();

        AssertEx.Equal("degraded", status.State);
        AssertEx.True(status.Reasons.Contains("unsafe_path"), string.Join(",", status.Reasons));
        AssertEx.True(status.Checks.Any(static check =>
            check.Name == "logs_directory"
            && check.RelativePath == "logs"
            && check.Reason == "unsafe_path"
            && check.Severity == "warning"));
    }

    public static void RuntimePreflightInspectDoesNotCreateMissingDirectories()
    {
        using var temp = TemporaryDirectory.Create();
        var service = new ProxyRuntimePreflightService(
            Provider(temp.Path),
            new ProxyDataDirectoryPathSafety(),
            new ProxyRuntimeDirectoryProbe(),
            TimeProvider.System);

        var status = service.Inspect();

        AssertEx.Equal("failed", status.State);
        AssertEx.True(status.Reasons.Contains("missing_directory"), string.Join(",", status.Reasons));
        AssertEx.False(Directory.Exists(Path.Combine(temp.Path, "config")));
        AssertEx.False(Directory.Exists(Path.Combine(temp.Path, "logs")));
    }

    private static MdravaDataDirectoryProvider Provider(string dataDirectory)
    {
        return new MdravaDataDirectoryProvider(new MdravaDataDirectoryOptions
        {
            DataDirectory = dataDirectory
        });
    }

    private sealed class DelegateProbe : IProxyRuntimeDirectoryProbe
    {
        private readonly Func<string, ProxyRuntimeDirectoryProbeResult> _probe;

        public DelegateProbe(Func<string, ProxyRuntimeDirectoryProbeResult> probe)
        {
            _probe = probe;
        }

        public ProxyRuntimeDirectoryProbeResult Probe(string path, bool createIfMissing)
        {
            return _probe(path);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow()
        {
            return _utcNow;
        }
    }

    private sealed class UnsafeLogsProvider : IMdravaDataDirectoryProvider
    {
        private readonly string _dataDirectory;
        private readonly string _logsDirectory;

        public UnsafeLogsProvider(string dataDirectory, string logsDirectory)
        {
            _dataDirectory = dataDirectory;
            _logsDirectory = logsDirectory;
        }

        public string GetDataDirectory() => _dataDirectory;

        public string GetProxyConfigDirectory() => Path.Combine(_dataDirectory, "config");

        public string GetSitesConfigDirectory() => Path.Combine(_dataDirectory, "config", "sites");

        public string GetProxyOperationalConfigPath() => Path.Combine(_dataDirectory, "config", "proxy.json");

        public string GetLogsDirectory() => _logsDirectory;

        public string GetCertificatesDirectory() => Path.Combine(_dataDirectory, "certs");

        public string GetStateDirectory() => Path.Combine(_dataDirectory, "state");
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
            var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"mdrava-preflight-tests-{Guid.NewGuid():N}");
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
