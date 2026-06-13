namespace MDRAVA.BLL.ControlPlane.Backup;

public sealed record ProxyBackupManifestCount(
    string Category,
    string Classification,
    int Count,
    long SizeBytes);
