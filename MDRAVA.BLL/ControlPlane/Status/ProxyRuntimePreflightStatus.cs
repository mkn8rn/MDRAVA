namespace MDRAVA.BLL.ControlPlane.Status;

public sealed record ProxyRuntimePreflightStatus
{
    private ProxyRuntimePreflightStatus(
        string state,
        DateTimeOffset? generatedAtUtc,
        IReadOnlyList<string> reasons,
        IReadOnlyList<ProxyRuntimePreflightCheck> checks)
    {
        State = state;
        GeneratedAtUtc = generatedAtUtc;
        Reasons = reasons;
        Checks = checks;
    }

    public string State { get; }

    public DateTimeOffset? GeneratedAtUtc { get; }

    public IReadOnlyList<string> Reasons { get; }

    public IReadOnlyList<ProxyRuntimePreflightCheck> Checks { get; }

    public static ProxyRuntimePreflightStatus Unknown { get; } = new(
        ProxyStatusText.Unknown,
        generatedAtUtc: null,
        reasons: [],
        checks: []);

    public static ProxyRuntimePreflightStatus Completed(
        DateTimeOffset generatedAtUtc,
        IReadOnlyList<ProxyRuntimePreflightCheck> checks,
        int maxReasons)
    {
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentOutOfRangeException.ThrowIfNegative(maxReasons);

        var failed = checks.Any(static check => string.Equals(
            check.Severity,
            ProxyStatusText.Error,
            StringComparison.OrdinalIgnoreCase));
        var degraded = checks.Any(static check => string.Equals(
            check.Severity,
            ProxyStatusText.Warning,
            StringComparison.OrdinalIgnoreCase));
        var state = failed
            ? ProxyStatusText.Failed
            : degraded ? ProxyStatusText.Degraded : ProxyStatusText.Healthy;
        var reasons = checks
            .Where(static check => !string.Equals(
                check.Reason,
                ProxyStatusText.Ok,
                StringComparison.OrdinalIgnoreCase))
            .Select(static check => check.Reason)
            .Where(static reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxReasons)
            .ToArray();

        return new ProxyRuntimePreflightStatus(
            state,
            generatedAtUtc,
            reasons,
            checks.ToArray());
    }
}

public sealed record ProxyRuntimePreflightCheck(
    string Name,
    string RelativePath,
    bool Exists,
    bool Created,
    bool CanRead,
    bool CanWrite,
    string Severity,
    string Reason);
