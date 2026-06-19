namespace MDRAVA.BLL.ControlPlane.Status;

public sealed record ProxyRuntimePreflightStatus
{
    private ProxyRuntimePreflightStatus(
        string state,
        DateTimeOffset? generatedAtUtc,
        IEnumerable<string> reasons,
        IEnumerable<ProxyRuntimePreflightCheck> checks)
    {
        State = state;
        GeneratedAtUtc = generatedAtUtc;
        Reasons = ProxyStatusList.Copy(reasons);
        Checks = ProxyStatusList.Copy(checks);
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
        IEnumerable<ProxyRuntimePreflightCheck> checks,
        int maxReasons)
    {
        var ownedChecks = ProxyStatusList.Copy(checks);
        ArgumentOutOfRangeException.ThrowIfNegative(maxReasons);

        var failed = ownedChecks.Any(static check => string.Equals(
            check.Severity,
            ProxyStatusText.Error,
            StringComparison.OrdinalIgnoreCase));
        var degraded = ownedChecks.Any(static check => string.Equals(
            check.Severity,
            ProxyStatusText.Warning,
            StringComparison.OrdinalIgnoreCase));
        var state = failed
            ? ProxyStatusText.Failed
            : degraded ? ProxyStatusText.Degraded : ProxyStatusText.Healthy;
        var reasons = ownedChecks
            .Where(static check => !string.Equals(
                check.Reason,
                ProxyStatusText.Ok,
                StringComparison.OrdinalIgnoreCase))
            .Select(static check => check.Reason)
            .Where(static reason => !string.IsNullOrWhiteSpace(reason))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(maxReasons);

        return new ProxyRuntimePreflightStatus(
            state,
            generatedAtUtc,
            reasons,
            ownedChecks);
    }
}

public sealed record ProxyRuntimePreflightCheck
{
    public ProxyRuntimePreflightCheck(
        string Name,
        string RelativePath,
        bool Exists,
        bool Created,
        bool CanRead,
        bool CanWrite,
        string Severity,
        string Reason)
    {
        ProxyStatusFacts.RequireText(Name, nameof(Name));
        ProxyStatusFacts.RequireText(RelativePath, nameof(RelativePath));
        ProxyStatusFacts.RequireText(Severity, nameof(Severity));
        ProxyStatusFacts.RequireText(Reason, nameof(Reason));

        this.Name = Name;
        this.RelativePath = RelativePath;
        this.Exists = Exists;
        this.Created = Created;
        this.CanRead = CanRead;
        this.CanWrite = CanWrite;
        this.Severity = Severity;
        this.Reason = Reason;
    }

    public string Name { get; }

    public string RelativePath { get; }

    public bool Exists { get; }

    public bool Created { get; }

    public bool CanRead { get; }

    public bool CanWrite { get; }

    public string Severity { get; }

    public string Reason { get; }
}
