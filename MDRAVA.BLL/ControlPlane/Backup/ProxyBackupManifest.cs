namespace MDRAVA.BLL.ControlPlane.Backup;

public sealed record ProxyBackupManifest(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<ProxyBackupDirectoryStatus> Directories,
    IReadOnlyList<ProxyBackupManifestEntry> Entries,
    IReadOnlyList<ProxyBackupManifestCount> Counts,
    IReadOnlyList<ProxyBackupWarning> Warnings,
    bool Truncated);
