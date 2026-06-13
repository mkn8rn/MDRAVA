using BusinessProxyBackupDirectoryStatus = MDRAVA.BLL.ControlPlane.Backup.ProxyBackupDirectoryStatus;
using BusinessProxyBackupManifestCount = MDRAVA.BLL.ControlPlane.Backup.ProxyBackupManifestCount;
using BusinessProxyBackupManifestEntry = MDRAVA.BLL.ControlPlane.Backup.ProxyBackupManifestEntry;
using BusinessProxyBackupWarning = MDRAVA.BLL.ControlPlane.Backup.ProxyBackupWarning;

namespace MDRAVA.API.Controllers;

public sealed record ProxyBackupDirectoryStatusResponse(
    string RelativePath,
    bool Exists,
    string Classification,
    bool Sensitive)
{
    public static IReadOnlyList<ProxyBackupDirectoryStatusResponse> FromStatuses(
        IReadOnlyList<BusinessProxyBackupDirectoryStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        return statuses.Select(FromStatus).ToArray();
    }

    private static ProxyBackupDirectoryStatusResponse FromStatus(BusinessProxyBackupDirectoryStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new ProxyBackupDirectoryStatusResponse(
            status.RelativePath,
            status.Exists,
            status.Classification,
            status.Sensitive);
    }
}

public sealed record ProxyBackupManifestEntryResponse(
    string RelativePath,
    string Category,
    string Classification,
    bool Sensitive,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc)
{
    public static IReadOnlyList<ProxyBackupManifestEntryResponse> FromEntries(
        IReadOnlyList<BusinessProxyBackupManifestEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries.Select(FromEntry).ToArray();
    }

    private static ProxyBackupManifestEntryResponse FromEntry(BusinessProxyBackupManifestEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new ProxyBackupManifestEntryResponse(
            entry.RelativePath,
            entry.Category,
            entry.Classification,
            entry.Sensitive,
            entry.SizeBytes,
            entry.LastWriteTimeUtc);
    }
}

public sealed record ProxyBackupManifestCountResponse(
    string Category,
    string Classification,
    int Count,
    long SizeBytes)
{
    public static IReadOnlyList<ProxyBackupManifestCountResponse> FromCounts(
        IReadOnlyList<BusinessProxyBackupManifestCount> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);

        return counts.Select(FromCount).ToArray();
    }

    private static ProxyBackupManifestCountResponse FromCount(BusinessProxyBackupManifestCount count)
    {
        ArgumentNullException.ThrowIfNull(count);

        return new ProxyBackupManifestCountResponse(
            count.Category,
            count.Classification,
            count.Count,
            count.SizeBytes);
    }
}

public sealed record ProxyBackupWarningResponse(
    string Code,
    string Message,
    string? RelativePath)
{
    public static IReadOnlyList<ProxyBackupWarningResponse> FromWarnings(
        IReadOnlyList<BusinessProxyBackupWarning> warnings)
    {
        ArgumentNullException.ThrowIfNull(warnings);

        return warnings.Select(FromWarning).ToArray();
    }

    private static ProxyBackupWarningResponse FromWarning(BusinessProxyBackupWarning warning)
    {
        ArgumentNullException.ThrowIfNull(warning);

        return new ProxyBackupWarningResponse(
            warning.Code,
            warning.Message,
            warning.RelativePath);
    }
}
