namespace MDRAVA.BLL.ControlPlane.Backup;

public static class ProxyRestoreValidationResponseBuilder
{
    public static ProxyRestoreValidationResponse Build(
        DateTimeOffset generatedAtUtc,
        int? activeConfigVersion,
        ProxyRestoreConfigurationValidationResult configValidation,
        ProxyBackupManifest manifest,
        IReadOnlyList<ProxyRestoreValidationFinding> errors,
        IReadOnlyList<ProxyRestoreValidationFinding> warnings,
        int maxFindings)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxFindings);
        return ProxyRestoreValidationResponse.Completed(
            generatedAtUtc,
            activeConfigVersion,
            configValidation,
            manifest,
            errors.Take(maxFindings).ToArray(),
            warnings.Take(maxFindings).ToArray());
    }
}
