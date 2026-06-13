namespace MDRAVA.BLL.ControlPlane.Backup;

public static class ProxyRestoreValidationResultBuilder
{
    public static ProxyRestoreValidationResult Build(
        DateTimeOffset generatedAtUtc,
        int? activeConfigVersion,
        ProxyRestoreConfigurationValidationResult configValidation,
        ProxyBackupManifest manifest,
        IReadOnlyList<ProxyRestoreValidationFinding> errors,
        IReadOnlyList<ProxyRestoreValidationFinding> warnings,
        int maxFindings)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxFindings);
        return ProxyRestoreValidationResult.Completed(
            generatedAtUtc,
            activeConfigVersion,
            configValidation,
            manifest,
            errors.Take(maxFindings).ToArray(),
            warnings.Take(maxFindings).ToArray());
    }
}
