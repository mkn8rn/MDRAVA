using BusinessProxyRestoreValidationFinding = MDRAVA.BLL.ControlPlane.Backup.ProxyRestoreValidationFinding;
using BusinessProxyRestoreValidationResult = MDRAVA.BLL.ControlPlane.Backup.ProxyRestoreValidationResult;

namespace MDRAVA.API.Controllers;

public sealed record ProxyRestoreValidationResponseBody(
    bool Succeeded,
    DateTimeOffset GeneratedAtUtc,
    int? ActiveConfigVersion,
    bool ConfigValidationSucceeded,
    int? WouldBeConfigVersion,
    ProxyBackupManifestResponse Manifest,
    IReadOnlyList<ProxyRestoreValidationFindingResponse> Errors,
    IReadOnlyList<ProxyRestoreValidationFindingResponse> Warnings)
{
    public static ProxyRestoreValidationResponseBody FromResult(BusinessProxyRestoreValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result switch
        {
            BusinessProxyRestoreValidationResult.AcceptedResult accepted => FromResult(accepted, succeeded: true),
            BusinessProxyRestoreValidationResult.RejectedResult rejected => FromResult(rejected, succeeded: false),
            _ => throw new InvalidOperationException($"Unknown restore validation result '{result.GetType().Name}'.")
        };
    }

    private static ProxyRestoreValidationResponseBody FromResult(
        BusinessProxyRestoreValidationResult result,
        bool succeeded)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ProxyRestoreValidationResponseBody(
            Succeeded: succeeded,
            GeneratedAtUtc: result.GeneratedAtUtc,
            ActiveConfigVersion: result.ActiveConfigVersion,
            ConfigValidationSucceeded: result.ConfigValidationSucceeded,
            WouldBeConfigVersion: result.WouldBeConfigVersion,
            Manifest: ProxyBackupManifestResponse.FromManifest(result.Manifest),
            Errors: ProxyRestoreValidationFindingResponse.FromFindings(result.Errors),
            Warnings: ProxyRestoreValidationFindingResponse.FromFindings(result.Warnings));
    }
}

public sealed record ProxyRestoreValidationFindingResponse(
    string Severity,
    string Code,
    string Message,
    string? RelativePath)
{
    public static IReadOnlyList<ProxyRestoreValidationFindingResponse> FromFindings(
        IReadOnlyList<BusinessProxyRestoreValidationFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        return findings.Select(FromFinding).ToArray();
    }

    private static ProxyRestoreValidationFindingResponse FromFinding(
        BusinessProxyRestoreValidationFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        return new ProxyRestoreValidationFindingResponse(
            finding.Severity,
            finding.Code,
            finding.Message,
            finding.RelativePath);
    }
}
