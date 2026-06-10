namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public sealed record ConfigLintRequest(
    string Format,
    string Text);

public sealed record ConfigLintResult(
    bool Succeeded,
    DateTimeOffset LintedAtUtc,
    ConfigLintSummary Summary,
    IReadOnlyList<ConfigLintFinding> Findings,
    IReadOnlyList<ProxyConfigurationFileError> ValidationErrors);

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

public sealed record ConfigLintStatus(
    bool Available,
    DateTimeOffset? LastActiveLintAtUtc,
    ConfigLintSummary? LastActiveLintSummary)
{
    public static ConfigLintStatus Empty { get; } = new(true, null, null);
}

