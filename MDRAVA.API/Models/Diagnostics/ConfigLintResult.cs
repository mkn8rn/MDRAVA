namespace MDRAVA.API.Models.Diagnostics;

public sealed record ConfigLintResult(
    bool Succeeded,
    DateTimeOffset LintedAtUtc,
    ConfigLintSummary Summary,
    IReadOnlyList<ConfigLintFinding> Findings,
    IReadOnlyList<ProxyConfigurationFileError> ValidationErrors);
