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
        var configValidationSucceeded = configValidation is ProxyRestoreConfigurationValidationResult.ValidResult;

        return new ProxyRestoreValidationResponse(
            configValidationSucceeded && errors.Count == 0,
            generatedAtUtc,
            activeConfigVersion,
            configValidationSucceeded,
            configValidation.WouldBeVersion,
            manifest,
            errors.Take(maxFindings).ToArray(),
            warnings.Take(maxFindings).ToArray());
    }
}
