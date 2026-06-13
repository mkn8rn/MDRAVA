namespace MDRAVA.BLL.ControlPlane.Backup;

public sealed record ProxyBackupWarning(
    string Code,
    string Message,
    string? RelativePath);
