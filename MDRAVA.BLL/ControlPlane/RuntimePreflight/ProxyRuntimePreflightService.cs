using MDRAVA.BLL.ControlPlane.Status;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.RuntimePreflight;

public sealed class ProxyRuntimePreflightService : IProxyStatusRuntimePreflightSource
{
    private const int MaxReasons = 12;
    private readonly IMdravaDataDirectoryProvider _dataDirectoryProvider;
    private readonly IProxyDataDirectoryPathSafety _pathSafety;
    private readonly IProxyRuntimeDirectoryProbe _directoryProbe;
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private ProxyRuntimePreflightStatus _lastStatus = ProxyRuntimePreflightStatus.Unknown;

    public ProxyRuntimePreflightService(
        IMdravaDataDirectoryProvider dataDirectoryProvider,
        IProxyDataDirectoryPathSafety pathSafety,
        IProxyRuntimeDirectoryProbe directoryProbe,
        TimeProvider timeProvider)
    {
        _dataDirectoryProvider = dataDirectoryProvider;
        _pathSafety = pathSafety;
        _directoryProbe = directoryProbe;
        _timeProvider = timeProvider;
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

    public ProxyRuntimePreflightStatus ReadRuntimePreflight()
    {
        return LastStatus;
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
        var generatedAtUtc = _timeProvider.GetUtcNow();
        var dataDirectory = _dataDirectoryProvider.GetDataDirectory();
        List<ProxyRuntimePreflightCheck> checks = [];

        foreach (var requirement in ProxyRuntimePreflightDirectoryPolicy.ExpectedDirectories())
        {
            checks.Add(Check(
                requirement,
                ResolveDirectoryPath(requirement.Kind),
                createMissingOwnedDirectories));
        }

        var failed = checks.Any(static check => string.Equals(check.Severity, ProxyStatusText.Error, StringComparison.OrdinalIgnoreCase));
        var degraded = checks.Any(static check => string.Equals(check.Severity, ProxyStatusText.Warning, StringComparison.OrdinalIgnoreCase));
        var state = failed ? ProxyStatusText.Failed : degraded ? ProxyStatusText.Degraded : ProxyStatusText.Healthy;
        var reasons = checks
            .Where(static check => !string.Equals(check.Reason, ProxyStatusText.Ok, StringComparison.OrdinalIgnoreCase))
            .Select(static check => check.Reason)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxReasons)
            .ToArray();
        return new ProxyRuntimePreflightStatus(state, generatedAtUtc, reasons, checks);

        ProxyRuntimePreflightCheck Check(
            ProxyRuntimePreflightDirectoryRequirement requirement,
            string path,
            bool createMissing)
        {
            if (requirement.Kind != ProxyRuntimePreflightDirectoryKind.Data
                && !_pathSafety.TryGetSafeRelativePath(dataDirectory, path, out _))
            {
                return new ProxyRuntimePreflightCheck(
                    requirement.Name,
                    requirement.RelativePath,
                    Exists: false,
                    Created: false,
                    CanRead: false,
                    CanWrite: false,
                    requirement.Critical ? ProxyStatusText.Error : ProxyStatusText.Warning,
                    "unsafe_path");
            }

            var result = _directoryProbe.Probe(path, createMissing);
            var classification = ProxyRuntimePreflightProbePolicy.Classify(result, requirement.Critical);
            return new ProxyRuntimePreflightCheck(
                requirement.Name,
                requirement.RelativePath,
                result.Exists,
                result.Created,
                result.CanRead,
                result.CanWrite,
                classification.Severity,
                classification.Reason);
        }
    }

    private string ResolveDirectoryPath(ProxyRuntimePreflightDirectoryKind kind)
    {
        return kind switch
        {
            ProxyRuntimePreflightDirectoryKind.Data => _dataDirectoryProvider.GetDataDirectory(),
            ProxyRuntimePreflightDirectoryKind.Config => _dataDirectoryProvider.GetProxyConfigDirectory(),
            ProxyRuntimePreflightDirectoryKind.Sites => _dataDirectoryProvider.GetSitesConfigDirectory(),
            ProxyRuntimePreflightDirectoryKind.Logs => _dataDirectoryProvider.GetLogsDirectory(),
            ProxyRuntimePreflightDirectoryKind.Certificates => _dataDirectoryProvider.GetCertificatesDirectory(),
            ProxyRuntimePreflightDirectoryKind.State => _dataDirectoryProvider.GetStateDirectory(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown runtime preflight directory kind.")
        };
    }
}
