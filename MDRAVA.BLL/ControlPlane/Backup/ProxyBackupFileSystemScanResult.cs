namespace MDRAVA.BLL.ControlPlane.Backup;

public sealed record ProxyBackupFileSystemScanResult
{
    private ProxyBackupFileSystemScanResult(
        bool rootExists,
        IReadOnlyList<ProxyBackupFileSystemEntry> files,
        IReadOnlyList<ProxyBackupFileSystemWarning> warnings)
    {
        RootExists = rootExists;
        Files = files;
        Warnings = warnings;
    }

    public bool RootExists { get; }

    public IReadOnlyList<ProxyBackupFileSystemEntry> Files { get; }

    public IReadOnlyList<ProxyBackupFileSystemWarning> Warnings { get; }

    public static ProxyBackupFileSystemScanResult MissingRoot()
    {
        return new ProxyBackupFileSystemScanResult(
            rootExists: false,
            files: [],
            warnings: []);
    }

    public static ProxyBackupFileSystemScanResult Scanned(
        IReadOnlyList<ProxyBackupFileSystemEntry> files,
        IReadOnlyList<ProxyBackupFileSystemWarning> warnings)
    {
        return new ProxyBackupFileSystemScanResult(
            rootExists: true,
            files: files,
            warnings: warnings);
    }
}
