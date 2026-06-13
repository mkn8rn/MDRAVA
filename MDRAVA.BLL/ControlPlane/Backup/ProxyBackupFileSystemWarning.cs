namespace MDRAVA.BLL.ControlPlane.Backup;

public sealed record ProxyBackupFileSystemWarning(
    string Code,
    string? RelativePath);
