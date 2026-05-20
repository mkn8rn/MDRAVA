namespace MDRAVA.API.Proxy.Backup;

public sealed record ProxyBackupManifestResponse(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<ProxyBackupDirectoryStatus> Directories,
    IReadOnlyList<ProxyBackupManifestEntry> Entries,
    IReadOnlyList<ProxyBackupManifestCount> Counts,
    IReadOnlyList<ProxyBackupWarning> Warnings,
    bool Truncated);

public sealed record ProxyBackupDirectoryStatus(
    string RelativePath,
    bool Exists,
    string Classification,
    bool Sensitive);

public sealed record ProxyBackupManifestEntry(
    string RelativePath,
    string Category,
    string Classification,
    bool Sensitive,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc);

public sealed record ProxyBackupManifestCount(
    string Category,
    string Classification,
    int Count,
    long SizeBytes);

public sealed record ProxyBackupWarning(
    string Code,
    string Message,
    string? RelativePath);

public sealed record ProxyRestoreValidationResponse(
    bool Succeeded,
    DateTimeOffset GeneratedAtUtc,
    int? ActiveConfigVersion,
    bool ConfigValidationSucceeded,
    int? WouldBeConfigVersion,
    ProxyBackupManifestResponse Manifest,
    IReadOnlyList<ProxyRestoreValidationFinding> Errors,
    IReadOnlyList<ProxyRestoreValidationFinding> Warnings);

public sealed record ProxyRestoreValidationFinding(
    string Severity,
    string Code,
    string Message,
    string? RelativePath);
