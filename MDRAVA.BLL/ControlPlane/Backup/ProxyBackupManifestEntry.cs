namespace MDRAVA.BLL.ControlPlane.Backup;

public sealed record ProxyBackupManifestEntry(
    string RelativePath,
    string Category,
    string Classification,
    bool Sensitive,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc);
