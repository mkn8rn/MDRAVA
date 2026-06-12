using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public static class ConfigLintResultBuilder
{
    public static ConfigLintResult Build(
        DateTimeOffset lintedAtUtc,
        IReadOnlyList<ConfigLintFinding> findings,
        IReadOnlyList<ProxyConfigurationFileError> validationErrors)
    {
        var summary = new ConfigLintSummary(
            findings.Count(static finding => string.Equals(finding.Severity, "info", StringComparison.OrdinalIgnoreCase)),
            findings.Count(static finding => string.Equals(finding.Severity, "warning", StringComparison.OrdinalIgnoreCase)),
            findings.Count(static finding => string.Equals(finding.Severity, "error", StringComparison.OrdinalIgnoreCase)));
        return new ConfigLintResult(summary.Error == 0, lintedAtUtc, summary, findings, validationErrors);
    }
}
