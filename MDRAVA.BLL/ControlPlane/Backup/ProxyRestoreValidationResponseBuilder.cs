namespace MDRAVA.BLL.ControlPlane.Backup;

public static class ProxyRestoreValidationResponseBuilder
{
    public static ProxyRestoreValidationResponse Build(
        DateTimeOffset generatedAtUtc,
        int? activeConfigVersion,
        ProxyRestoreConfigurationValidationResult configValidation,
        ProxyBackupManifestResponse manifest,
        IReadOnlyList<ProxyRestoreValidationFinding> errors,
        IReadOnlyList<ProxyRestoreValidationFinding> warnings,
        int maxFindings)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxFindings);

        return new ProxyRestoreValidationResponse(
            configValidation.Succeeded && errors.Count == 0,
            generatedAtUtc,
            activeConfigVersion,
            configValidation.Succeeded,
            configValidation.WouldBeVersion,
            manifest,
            errors.Take(maxFindings).ToArray(),
            warnings.Take(maxFindings).ToArray());
    }
}
