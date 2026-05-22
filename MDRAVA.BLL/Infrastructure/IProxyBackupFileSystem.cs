namespace MDRAVA.BLL.Infrastructure;

public interface IProxyBackupFileSystem
{
    bool DirectoryExists(string root, string relativePath);

    ProxyBackupFileSystemScanResult ScanDataDirectory(string root);

    bool TryGetSafeRelativePath(string root, string path, out string relativePath);
}

public sealed record ProxyBackupFileSystemScanResult(
    bool RootExists,
    IReadOnlyList<ProxyBackupFileSystemEntry> Files,
    IReadOnlyList<ProxyBackupFileSystemWarning> Warnings);

public sealed record ProxyBackupFileSystemEntry(
    string RelativePath,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc);

public sealed record ProxyBackupFileSystemWarning(
    string Code,
    string? RelativePath);
