using MDRAVA.BLL.ControlPlane.Backup;

namespace MDRAVA.API.Controllers;

public sealed record ProxyBackupManifestResponse(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<ProxyBackupDirectoryStatus> Directories,
    IReadOnlyList<ProxyBackupManifestEntry> Entries,
    IReadOnlyList<ProxyBackupManifestCount> Counts,
    IReadOnlyList<ProxyBackupWarning> Warnings,
    bool Truncated)
{
    public static ProxyBackupManifestResponse FromManifest(ProxyBackupManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return new ProxyBackupManifestResponse(
            GeneratedAtUtc: manifest.GeneratedAtUtc,
            Directories: manifest.Directories,
            Entries: manifest.Entries,
            Counts: manifest.Counts,
            Warnings: manifest.Warnings,
            Truncated: manifest.Truncated);
    }
}
