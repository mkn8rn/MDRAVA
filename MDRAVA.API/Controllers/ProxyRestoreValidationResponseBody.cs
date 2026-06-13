using MDRAVA.BLL.ControlPlane.Backup;

namespace MDRAVA.API.Controllers;

public sealed record ProxyRestoreValidationResponseBody(
    bool Succeeded,
    DateTimeOffset GeneratedAtUtc,
    int? ActiveConfigVersion,
    bool ConfigValidationSucceeded,
    int? WouldBeConfigVersion,
    ProxyBackupManifestResponse Manifest,
    IReadOnlyList<ProxyRestoreValidationFinding> Errors,
    IReadOnlyList<ProxyRestoreValidationFinding> Warnings)
{
    public static ProxyRestoreValidationResponseBody FromResult(ProxyRestoreValidationResponse result)
    {
        return result switch
        {
            ProxyRestoreValidationResponse.AcceptedResult accepted => FromResult(accepted, succeeded: true),
            ProxyRestoreValidationResponse.RejectedResult rejected => FromResult(rejected, succeeded: false),
            _ => throw new InvalidOperationException($"Unknown restore validation result '{result.GetType().Name}'.")
        };
    }

    private static ProxyRestoreValidationResponseBody FromResult(
        ProxyRestoreValidationResponse result,
        bool succeeded)
    {
        var configValidationSucceeded = result.ConfigValidation is ProxyRestoreConfigurationValidationResult.ValidResult;

        return new ProxyRestoreValidationResponseBody(
            Succeeded: succeeded,
            GeneratedAtUtc: result.GeneratedAtUtc,
            ActiveConfigVersion: result.ActiveConfigVersion,
            ConfigValidationSucceeded: configValidationSucceeded,
            WouldBeConfigVersion: result.WouldBeConfigVersion,
            Manifest: ProxyBackupManifestResponse.FromManifest(result.Manifest),
            Errors: result.Errors,
            Warnings: result.Warnings);
    }
}
