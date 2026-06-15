using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public abstract partial record ConfigLintResult
{
    private ConfigLintResult(
        DateTimeOffset lintedAtUtc,
        ConfigLintSummary summary,
        IReadOnlyList<ConfigLintFinding> findings,
        IReadOnlyList<ProxyConfigurationFileError> validationErrors)
    {
        ArgumentNullException.ThrowIfNull(findings);
        ArgumentNullException.ThrowIfNull(validationErrors);

        LintedAtUtc = lintedAtUtc;
        Summary = summary;
        Findings = ConfigLintList.Copy(findings);
        ValidationErrors = ConfigLintList.Copy(validationErrors);
    }

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

        return summary.Error == 0
            ? new AcceptedResult(lintedAtUtc, summary, findings, validationErrors)
            : new RejectedResult(lintedAtUtc, summary, findings, validationErrors);
    }
}
