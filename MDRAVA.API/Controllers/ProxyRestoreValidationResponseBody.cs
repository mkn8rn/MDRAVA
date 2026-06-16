using BusinessProxyRestoreValidationFinding = MDRAVA.BLL.ControlPlane.Backup.ProxyRestoreValidationFinding;
using BusinessProxyRestoreValidationResult = MDRAVA.BLL.ControlPlane.Backup.ProxyRestoreValidationResult;

namespace MDRAVA.API.Controllers;

public sealed record ProxyRestoreValidationResponseBody
{
    public ProxyRestoreValidationResponseBody(
        bool succeeded,
        DateTimeOffset generatedAtUtc,
        int? activeConfigVersion,
        bool configValidationSucceeded,
        int? wouldBeConfigVersion,
        ProxyBackupManifestResponse manifest,
        IReadOnlyList<ProxyRestoreValidationFindingResponse> errors,
        IReadOnlyList<ProxyRestoreValidationFindingResponse> warnings)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        Succeeded = succeeded;
        GeneratedAtUtc = generatedAtUtc;
        ActiveConfigVersion = activeConfigVersion;
        ConfigValidationSucceeded = configValidationSucceeded;
        WouldBeConfigVersion = wouldBeConfigVersion;
        Manifest = manifest;
        Errors = ApiResponseList.Copy(errors);
        Warnings = ApiResponseList.Copy(warnings);
    }

    public bool Succeeded { get; }

    public DateTimeOffset GeneratedAtUtc { get; }

    public int? ActiveConfigVersion { get; }

    public bool ConfigValidationSucceeded { get; }

    public int? WouldBeConfigVersion { get; }

    public ProxyBackupManifestResponse Manifest { get; }

    public IReadOnlyList<ProxyRestoreValidationFindingResponse> Errors { get; }

    public IReadOnlyList<ProxyRestoreValidationFindingResponse> Warnings { get; }

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
            succeeded: succeeded,
            generatedAtUtc: result.GeneratedAtUtc,
            activeConfigVersion: result.ActiveConfigVersion,
            configValidationSucceeded: result.ConfigValidationSucceeded,
            wouldBeConfigVersion: result.WouldBeConfigVersion,
            manifest: ProxyBackupManifestResponse.FromManifest(result.Manifest),
            errors: ProxyRestoreValidationFindingResponse.FromFindings(result.Errors),
            warnings: ProxyRestoreValidationFindingResponse.FromFindings(result.Warnings));
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

        return ApiResponseList.Copy(findings.Select(FromFinding));
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
