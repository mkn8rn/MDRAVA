namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public sealed record ConfigLintRequest(
    string? Format,
    string? Text);
