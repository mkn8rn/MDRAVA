using MDRAVA.BLL.Configuration;
using System.Text.Json.Serialization;

namespace MDRAVA.BLL.ControlPlane.Backup;

public interface IProxyRestoreConfigurationValidator
{
    ValueTask<ProxyRestoreConfigurationValidationResult> ValidateExistingLayoutAsync(CancellationToken cancellationToken);
}

public abstract record ProxyRestoreConfigurationValidationResult
{
    private ProxyRestoreConfigurationValidationResult()
    {
    }

    public abstract IReadOnlyList<string> Errors { get; }

    public abstract IReadOnlyList<ProxyConfigurationFileError> FileErrors { get; }

    public abstract int? WouldBeVersion { get; }

    public static ProxyRestoreConfigurationValidationResult Completed(
        IReadOnlyList<string> errors,
        IReadOnlyList<ProxyConfigurationFileError> fileErrors,
        int? wouldBeVersion)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(fileErrors);

        return errors.Count == 0 && fileErrors.Count == 0
            ? new ValidResult(wouldBeVersion)
            : new InvalidResult(errors, fileErrors, wouldBeVersion);
    }

    public sealed record ValidResult : ProxyRestoreConfigurationValidationResult
    {
        public ValidResult(int? wouldBeVersion)
        {
            WouldBeVersion = wouldBeVersion;
        }

        public override IReadOnlyList<string> Errors => [];

        public override IReadOnlyList<ProxyConfigurationFileError> FileErrors => [];

        public override int? WouldBeVersion { get; }
    }

    public sealed record InvalidResult : ProxyRestoreConfigurationValidationResult
    {
        public InvalidResult(
            IReadOnlyList<string> errors,
            IReadOnlyList<ProxyConfigurationFileError> fileErrors,
            int? wouldBeVersion)
        {
            ArgumentNullException.ThrowIfNull(errors);
            ArgumentNullException.ThrowIfNull(fileErrors);
            if (errors.Count == 0 && fileErrors.Count == 0)
            {
                throw new ArgumentException("Invalid restore configuration validation requires at least one error.");
            }

            Errors = errors;
            FileErrors = fileErrors;
            WouldBeVersion = wouldBeVersion;
        }

        public override IReadOnlyList<string> Errors { get; }

        public override IReadOnlyList<ProxyConfigurationFileError> FileErrors { get; }

        public override int? WouldBeVersion { get; }
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

public sealed record ProxyBackupManifest(
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

public abstract record ProxyRestoreValidationResponse
{
    private ProxyRestoreValidationResponse(
        DateTimeOffset generatedAtUtc,
        int? activeConfigVersion,
        ProxyRestoreConfigurationValidationResult configValidation,
        ProxyBackupManifest manifest,
        IReadOnlyList<ProxyRestoreValidationFinding> errors,
        IReadOnlyList<ProxyRestoreValidationFinding> warnings)
    {
        ArgumentNullException.ThrowIfNull(configValidation);
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(warnings);

        GeneratedAtUtc = generatedAtUtc;
        ActiveConfigVersion = activeConfigVersion;
        ConfigValidation = configValidation;
        Manifest = manifest;
        Errors = errors;
        Warnings = warnings;
    }

    public DateTimeOffset GeneratedAtUtc { get; }

    public int? ActiveConfigVersion { get; }

    [JsonIgnore]
    public ProxyRestoreConfigurationValidationResult ConfigValidation { get; }

    public int? WouldBeConfigVersion => ConfigValidation.WouldBeVersion;

    public ProxyBackupManifest Manifest { get; }

    public IReadOnlyList<ProxyRestoreValidationFinding> Errors { get; }

    public IReadOnlyList<ProxyRestoreValidationFinding> Warnings { get; }

    public static ProxyRestoreValidationResponse Completed(
        DateTimeOffset generatedAtUtc,
        int? activeConfigVersion,
        ProxyRestoreConfigurationValidationResult configValidation,
        ProxyBackupManifest manifest,
        IReadOnlyList<ProxyRestoreValidationFinding> errors,
        IReadOnlyList<ProxyRestoreValidationFinding> warnings)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return configValidation is ProxyRestoreConfigurationValidationResult.ValidResult && errors.Count == 0
            ? new AcceptedResult(
                generatedAtUtc,
                activeConfigVersion,
                configValidation,
                manifest,
                errors,
                warnings)
            : new RejectedResult(
                generatedAtUtc,
                activeConfigVersion,
                configValidation,
                manifest,
                errors,
                warnings);
    }

    public sealed record AcceptedResult : ProxyRestoreValidationResponse
    {
        internal AcceptedResult(
            DateTimeOffset generatedAtUtc,
            int? activeConfigVersion,
            ProxyRestoreConfigurationValidationResult configValidation,
            ProxyBackupManifest manifest,
            IReadOnlyList<ProxyRestoreValidationFinding> errors,
            IReadOnlyList<ProxyRestoreValidationFinding> warnings)
            : base(generatedAtUtc, activeConfigVersion, configValidation, manifest, errors, warnings)
        {
        }
    }

    public sealed record RejectedResult : ProxyRestoreValidationResponse
    {
        internal RejectedResult(
            DateTimeOffset generatedAtUtc,
            int? activeConfigVersion,
            ProxyRestoreConfigurationValidationResult configValidation,
            ProxyBackupManifest manifest,
            IReadOnlyList<ProxyRestoreValidationFinding> errors,
            IReadOnlyList<ProxyRestoreValidationFinding> warnings)
            : base(generatedAtUtc, activeConfigVersion, configValidation, manifest, errors, warnings)
        {
        }
    }
}

public sealed record ProxyRestoreValidationFinding(
    string Severity,
    string Code,
    string Message,
    string? RelativePath);
