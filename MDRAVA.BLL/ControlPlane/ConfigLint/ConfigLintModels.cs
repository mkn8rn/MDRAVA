using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public sealed record ConfigLintRequest(
    string? Format,
    string? Text);

public sealed record ConfigLintResult
{
    private ConfigLintResult(
        bool succeeded,
        DateTimeOffset lintedAtUtc,
        ConfigLintSummary summary,
        IReadOnlyList<ConfigLintFinding> findings,
        IReadOnlyList<ProxyConfigurationFileError> validationErrors)
    {
        Succeeded = succeeded;
        LintedAtUtc = lintedAtUtc;
        Summary = summary;
        Findings = findings;
        ValidationErrors = validationErrors;
    }

    public bool Succeeded { get; }

    public DateTimeOffset LintedAtUtc { get; }

    public ConfigLintSummary Summary { get; }

    public IReadOnlyList<ConfigLintFinding> Findings { get; }

    public IReadOnlyList<ProxyConfigurationFileError> ValidationErrors { get; }

    public static ConfigLintResult Completed(
        DateTimeOffset lintedAtUtc,
        IReadOnlyList<ConfigLintFinding> findings,
        IReadOnlyList<ProxyConfigurationFileError> validationErrors)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(validationErrors);

        var summary = new ConfigLintSummary(
            findings.Count(static finding => string.Equals(finding.Severity, "info", StringComparison.OrdinalIgnoreCase)),
            findings.Count(static finding => string.Equals(finding.Severity, "warning", StringComparison.OrdinalIgnoreCase)),
            findings.Count(static finding => string.Equals(finding.Severity, "error", StringComparison.OrdinalIgnoreCase)));
        return new ConfigLintResult(summary.Error == 0, lintedAtUtc, summary, findings, validationErrors);
    }
}

public sealed record ConfigLintFinding(
    string Severity,
    string Code,
    string Message,
    string? Source,
    string? Path,
    string? SuggestedFix);

public sealed record ConfigLintSummary(
    int Info,
    int Warning,
    int Error)
{
    public static ConfigLintSummary Empty { get; } = new(0, 0, 0);
}

public sealed record ConfigLintStatus
{
    public static ConfigLintStatus Empty { get; } = new(
        available: true,
        lastActiveLintAtUtc: null,
        lastActiveLintSummary: null);

    private ConfigLintStatus(
        bool available,
        DateTimeOffset? lastActiveLintAtUtc,
        ConfigLintSummary? lastActiveLintSummary)
    {
        Available = available;
        LastActiveLintAtUtc = lastActiveLintAtUtc;
        LastActiveLintSummary = lastActiveLintSummary;
    }

    public bool Available { get; }

    public DateTimeOffset? LastActiveLintAtUtc { get; }

    public ConfigLintSummary? LastActiveLintSummary { get; }

    public static ConfigLintStatus Completed(
        DateTimeOffset lintedAtUtc,
        ConfigLintSummary summary)
    {
        return new ConfigLintStatus(
            available: true,
            lastActiveLintAtUtc: lintedAtUtc,
            lastActiveLintSummary: summary);
    }
}
