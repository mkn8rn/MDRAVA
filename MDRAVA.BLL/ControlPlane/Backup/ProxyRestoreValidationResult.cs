namespace MDRAVA.BLL.ControlPlane.Backup;

public abstract record ProxyRestoreValidationResult
{
    private ProxyRestoreValidationResult(
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
        ConfigValidationSucceeded = configValidation is ProxyRestoreConfigurationValidationResult.ValidResult;
        WouldBeConfigVersion = configValidation.WouldBeVersion;
        Manifest = manifest;
        Errors = errors.ToArray();
        Warnings = warnings.ToArray();
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

    public sealed record AcceptedResult : ProxyRestoreValidationResult
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

    public sealed record RejectedResult : ProxyRestoreValidationResult
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
