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
    public static ProxyRestoreValidationResponseBody FromResult(ProxyRestoreValidationResult result)
    {
        return result switch
        {
            ProxyRestoreValidationResult.AcceptedResult accepted => FromResult(accepted, succeeded: true),
            ProxyRestoreValidationResult.RejectedResult rejected => FromResult(rejected, succeeded: false),
            _ => throw new InvalidOperationException($"Unknown restore validation result '{result.GetType().Name}'.")
        };
    }

    private static ProxyRestoreValidationResponseBody FromResult(
        ProxyRestoreValidationResult result,
        bool succeeded)
    {
        return new ProxyRestoreValidationResponseBody(
            Succeeded: succeeded,
            GeneratedAtUtc: result.GeneratedAtUtc,
            ActiveConfigVersion: result.ActiveConfigVersion,
            ConfigValidationSucceeded: result.ConfigValidationSucceeded,
            WouldBeConfigVersion: result.WouldBeConfigVersion,
            Manifest: ProxyBackupManifestResponse.FromManifest(result.Manifest),
            Errors: result.Errors,
            Warnings: result.Warnings);
    }
}
