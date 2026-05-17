namespace MDRAVA.API.Models.Diagnostics;

public sealed record ConfigLintFinding(
    string Severity,
    string Code,
    string Message,
    string? Source,
    string? Path,
    string? SuggestedFix);
