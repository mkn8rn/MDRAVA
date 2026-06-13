namespace MDRAVA.BLL.ControlPlane.Backup;

public sealed record ProxyBackupDirectoryStatus(
    string RelativePath,
    bool Exists,
    string Classification,
    bool Sensitive);
