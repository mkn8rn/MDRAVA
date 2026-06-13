namespace MDRAVA.BLL.ControlPlane.Backup;

public sealed record ProxyBackupFileSystemEntry(
    string RelativePath,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc);
