namespace MDRAVA.BLL.ControlPlane.Backup;

public abstract record ProxyRestoreValidationResult
{
    private ProxyRestoreValidationResult(
        DateTimeOffset generatedAtUtc,
        int? activeConfigVersion,
        ProxyRestoreConfigurationValidationResult configValidation,
        ProxyBackupManifest manifest,
        IEnumerable<ProxyRestoreValidationFinding> errors,
        IEnumerable<ProxyRestoreValidationFinding> warnings)
    {
        ArgumentNullException.ThrowIfNull(configValidation);
        ArgumentNullException.ThrowIfNull(manifest);
        GeneratedAtUtc = generatedAtUtc;
        ActiveConfigVersion = activeConfigVersion;
        ConfigValidationSucceeded = configValidation is ProxyRestoreConfigurationValidationResult.ValidResult;
        WouldBeConfigVersion = configValidation.WouldBeVersion;
        Manifest = manifest;
        Errors = BackupList.Copy(errors);
        Warnings = BackupList.Copy(warnings);
    }

    public DateTimeOffset GeneratedAtUtc { get; }

    public int? ActiveConfigVersion { get; }

    public bool ConfigValidationSucceeded { get; }

    public int? WouldBeConfigVersion { get; }

    public ProxyBackupManifest Manifest { get; }

    public IReadOnlyList<ProxyRestoreValidationFinding> Errors { get; }

    public IReadOnlyList<ProxyRestoreValidationFinding> Warnings { get; }

    public static ProxyRestoreValidationResult Completed(
        DateTimeOffset generatedAtUtc,
        int? activeConfigVersion,
        ProxyRestoreConfigurationValidationResult configValidation,
        ProxyBackupManifest manifest,
        IEnumerable<ProxyRestoreValidationFinding> errors,
        IEnumerable<ProxyRestoreValidationFinding> warnings)
    {
        var ownedErrors = BackupList.Copy(errors);

        return configValidation is ProxyRestoreConfigurationValidationResult.ValidResult && ownedErrors.Count == 0
            ? new AcceptedResult(
                generatedAtUtc,
                activeConfigVersion,
                configValidation,
                manifest,
                ownedErrors,
                warnings)
            : new RejectedResult(
                generatedAtUtc,
                activeConfigVersion,
                configValidation,
                manifest,
                ownedErrors,
                warnings);
    }

    public sealed record AcceptedResult : ProxyRestoreValidationResult
    {
        internal AcceptedResult(
            DateTimeOffset generatedAtUtc,
            int? activeConfigVersion,
            ProxyRestoreConfigurationValidationResult configValidation,
            ProxyBackupManifest manifest,
            IEnumerable<ProxyRestoreValidationFinding> errors,
            IEnumerable<ProxyRestoreValidationFinding> warnings)
            : base(generatedAtUtc, activeConfigVersion, configValidation, manifest, errors, warnings)
        {
        }
    }

    public sealed record RejectedResult : ProxyRestoreValidationResult
    {
        internal RejectedResult(
            DateTimeOffset generatedAtUtc,
            int? activeConfigVersion,
            ProxyRestoreConfigurationValidationResult configValidation,
            ProxyBackupManifest manifest,
            IEnumerable<ProxyRestoreValidationFinding> errors,
            IEnumerable<ProxyRestoreValidationFinding> warnings)
            : base(generatedAtUtc, activeConfigVersion, configValidation, manifest, errors, warnings)
        {
        }
    }
}
