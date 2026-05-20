using MDRAVA.API.Proxy.Backup;
using MDRAVA.API.Proxy.Configuration.Paths;

namespace MDRAVA.API.Proxy.Runtime;

public sealed class ProxyRuntimePreflightService
{
    private const int MaxReasons = 12;
    private readonly IMdravaDataDirectoryProvider _dataDirectoryProvider;
    private readonly IProxyRuntimeDirectoryProbe _directoryProbe;
    private readonly object _gate = new();
    private ProxyRuntimePreflightStatus _lastStatus = ProxyRuntimePreflightStatus.Unknown;

    public ProxyRuntimePreflightService(
        IMdravaDataDirectoryProvider dataDirectoryProvider,
        IProxyRuntimeDirectoryProbe directoryProbe)
    {
        _dataDirectoryProvider = dataDirectoryProvider;
        _directoryProbe = directoryProbe;
    }

    public ProxyRuntimePreflightStatus LastStatus
    {
        get
        {
            lock (_gate)
            {
                return _lastStatus;
            }
        }
    }

    public ProxyRuntimePreflightStatus RunStartupChecks()
    {
        var status = Build(createMissingOwnedDirectories: true);
        lock (_gate)
        {
            _lastStatus = status;
        }

        return status;
    }

    public ProxyRuntimePreflightStatus Inspect()
    {
        return Build(createMissingOwnedDirectories: false);
    }

    private ProxyRuntimePreflightStatus Build(bool createMissingOwnedDirectories)
    {
        var generatedAtUtc = DateTimeOffset.UtcNow;
        var dataDirectory = _dataDirectoryProvider.GetDataDirectory();
        List<ProxyRuntimePreflightCheck> checks = [];

        var data = Check("data_directory", dataDirectory, ".", critical: true, createMissingOwnedDirectories);
        checks.Add(data);
        checks.Add(Check("config_directory", _dataDirectoryProvider.GetProxyConfigDirectory(), "config", critical: true, createMissingOwnedDirectories));
        checks.Add(Check("sites_directory", _dataDirectoryProvider.GetSitesConfigDirectory(), "config/sites", critical: true, createMissingOwnedDirectories));
        checks.Add(Check("logs_directory", _dataDirectoryProvider.GetLogsDirectory(), "logs", critical: false, createMissingOwnedDirectories));
        checks.Add(Check("certificates_directory", _dataDirectoryProvider.GetCertificatesDirectory(), "certs", critical: false, createMissingOwnedDirectories));
        checks.Add(Check("state_directory", _dataDirectoryProvider.GetStateDirectory(), "state", critical: false, createMissingOwnedDirectories));

        var failed = checks.Any(static check => string.Equals(check.Severity, "error", StringComparison.OrdinalIgnoreCase));
        var degraded = checks.Any(static check => string.Equals(check.Severity, "warning", StringComparison.OrdinalIgnoreCase));
        var state = failed ? "failed" : degraded ? "degraded" : "healthy";
        var reasons = checks
            .Where(static check => !string.Equals(check.Reason, "ok", StringComparison.OrdinalIgnoreCase))
            .Select(static check => check.Reason)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxReasons)
            .ToArray();
        return new ProxyRuntimePreflightStatus(state, generatedAtUtc, reasons, checks);

        ProxyRuntimePreflightCheck Check(
            string name,
            string path,
            string relativePath,
            bool critical,
            bool createMissing)
        {
            if (name != "data_directory"
                && !ProxyBackupPathSafety.TryGetSafeRelativePath(dataDirectory, path, out var safeRelativePath))
            {
                return new ProxyRuntimePreflightCheck(
                    name,
                    relativePath,
                    Exists: false,
                    Created: false,
                    CanRead: false,
                    CanWrite: false,
                    critical ? "error" : "warning",
                    "unsafe_path");
            }

            var result = _directoryProbe.Probe(path, createMissing);
            var reason = Reason(result);
            var severity = string.Equals(reason, "ok", StringComparison.OrdinalIgnoreCase)
                ? "info"
                : critical ? "error" : "warning";
            return new ProxyRuntimePreflightCheck(
                name,
                name == "data_directory" ? "." : relativePath,
                result.Exists,
                result.Created,
                result.CanRead,
                result.CanWrite,
                severity,
                reason);
        }
    }

    private static string Reason(ProxyRuntimeDirectoryProbeResult result)
    {
        if (string.Equals(result.FailureReason, "access_denied", StringComparison.OrdinalIgnoreCase))
        {
            return "directory_access_denied";
        }

        if (string.Equals(result.FailureReason, "io_error", StringComparison.OrdinalIgnoreCase))
        {
            return "directory_io_error";
        }

        if (!result.Exists)
        {
            return "missing_directory";
        }

        if (!result.CanRead)
        {
            return "directory_not_readable";
        }

        if (!result.CanWrite)
        {
            return "directory_not_writable";
        }

        return "ok";
    }
}
