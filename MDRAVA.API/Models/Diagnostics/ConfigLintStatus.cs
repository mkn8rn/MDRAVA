namespace MDRAVA.API.Models.Diagnostics;

public sealed record ConfigLintStatus(
    bool Available,
    DateTimeOffset? LastActiveLintAtUtc,
    ConfigLintSummary? LastActiveLintSummary)
{
    public static ConfigLintStatus Empty { get; } = new(true, null, null);
}
