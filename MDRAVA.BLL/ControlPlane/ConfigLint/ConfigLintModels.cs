using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public sealed record ConfigLintRequest(
    string? Format,
    string? Text);

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
