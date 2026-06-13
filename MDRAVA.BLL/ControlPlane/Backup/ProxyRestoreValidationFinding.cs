namespace MDRAVA.BLL.ControlPlane.Backup;

public sealed record ProxyRestoreValidationFinding(
    string Severity,
    string Code,
    string Message,
    string? RelativePath);
