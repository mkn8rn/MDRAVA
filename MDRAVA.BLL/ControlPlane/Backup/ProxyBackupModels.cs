using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Backup;

public interface IProxyRestoreConfigurationValidator
{
    ValueTask<ProxyRestoreConfigurationValidationResult> ValidateExistingLayoutAsync(CancellationToken cancellationToken);
}

public sealed record ProxyRestoreConfigurationValidationResult
{
    private ProxyRestoreConfigurationValidationResult(
        bool succeeded,
        IReadOnlyList<string> errors,
        IReadOnlyList<ProxyConfigurationFileError> fileErrors,
        int? wouldBeVersion)
    {
        Succeeded = succeeded;
        Errors = errors;
        FileErrors = fileErrors;
        WouldBeVersion = wouldBeVersion;
    }

    public bool Succeeded { get; }

    public IReadOnlyList<string> Errors { get; }

    public IReadOnlyList<ProxyConfigurationFileError> FileErrors { get; }

    public int? WouldBeVersion { get; }

    public static ProxyRestoreConfigurationValidationResult Completed(
        IReadOnlyList<string> errors,
        IReadOnlyList<ProxyConfigurationFileError> fileErrors,
        int? wouldBeVersion)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(fileErrors);

        return new ProxyRestoreConfigurationValidationResult(
            errors.Count == 0 && fileErrors.Count == 0,
            errors,
            fileErrors,
            wouldBeVersion);
    }
}

public interface IProxyBackupFileSystem
{
    bool DirectoryExists(string root, string relativePath);

    ProxyBackupFileSystemScanResult ScanDataDirectory(string root);

    ProxySafeRelativePathResult GetSafeRelativePath(string root, string path);
}

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

public sealed record ProxyBackupFileSystemEntry(
    string RelativePath,
    long SizeBytes,
    DateTimeOffset LastWriteTimeUtc);

public sealed record ProxyBackupFileSystemWarning(
    string Code,
    string? RelativePath);

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
