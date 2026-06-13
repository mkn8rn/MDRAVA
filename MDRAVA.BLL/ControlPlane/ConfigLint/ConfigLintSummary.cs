namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public sealed record ConfigLintSummary(
    int Info,
    int Warning,
    int Error)
{
    public static ConfigLintSummary Empty { get; } = new(0, 0, 0);
}
