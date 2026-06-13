using BusinessProxyBackupManifest = MDRAVA.BLL.ControlPlane.Backup.ProxyBackupManifest;

namespace MDRAVA.API.Controllers;

public sealed record ProxyBackupManifestResponse(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<ProxyBackupDirectoryStatusResponse> Directories,
    IReadOnlyList<ProxyBackupManifestEntryResponse> Entries,
    IReadOnlyList<ProxyBackupManifestCountResponse> Counts,
    IReadOnlyList<ProxyBackupWarningResponse> Warnings,
    bool Truncated)
{
    public static ProxyBackupManifestResponse FromManifest(BusinessProxyBackupManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        return new ProxyBackupManifestResponse(
            GeneratedAtUtc: manifest.GeneratedAtUtc,
            Directories: ProxyBackupDirectoryStatusResponse.FromStatuses(manifest.Directories),
            Entries: ProxyBackupManifestEntryResponse.FromEntries(manifest.Entries),
            Counts: ProxyBackupManifestCountResponse.FromCounts(manifest.Counts),
            Warnings: ProxyBackupWarningResponse.FromWarnings(manifest.Warnings),
            Truncated: manifest.Truncated);
    }
}
