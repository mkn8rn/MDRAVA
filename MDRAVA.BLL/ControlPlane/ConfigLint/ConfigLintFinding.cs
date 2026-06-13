namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public sealed record ConfigLintFinding(
    string Severity,
    string Code,
    string Message,
    string? Source,
    string? Path,
    string? SuggestedFix);
