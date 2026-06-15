using BusinessProxyConfigurationValidationResult =
    MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationValidationResult;

namespace MDRAVA.API.Controllers;

public sealed record ProxyConfigurationValidationResponse(
    bool Succeeded,
    string SourceDirectory,
    DateTimeOffset AttemptedAtUtc,
    int? ActiveVersion,
    DateTimeOffset? LastSuccessfulLoadAtUtc,
    int? WouldBeVersion,
    IReadOnlyList<string> SourceFiles,
    ProxyConfigurationDiscoveryResponse Discovery,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ProxyConfigurationFileErrorResponse> FileErrors)
{
    public static ProxyConfigurationValidationResponse FromResult(BusinessProxyConfigurationValidationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result switch
        {
            BusinessProxyConfigurationValidationResult.ValidResult valid => FromResult(valid, succeeded: true),
            BusinessProxyConfigurationValidationResult.InvalidResult invalid => FromResult(invalid, succeeded: false),
            _ => throw new InvalidOperationException($"Unknown validation result '{result.GetType().Name}'.")
        };
    }

    private static ProxyConfigurationValidationResponse FromResult(
        BusinessProxyConfigurationValidationResult result,
        bool succeeded)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ProxyConfigurationValidationResponse(
            Succeeded: succeeded,
            SourceDirectory: result.SourceDirectory,
            AttemptedAtUtc: result.AttemptedAtUtc,
            ActiveVersion: result.ActiveVersion,
            LastSuccessfulLoadAtUtc: result.LastSuccessfulLoadAtUtc,
            WouldBeVersion: result.WouldBeVersion,
            SourceFiles: ApiResponseList.Copy(result.SourceFiles),
            Discovery: ProxyConfigurationDiscoveryResponse.FromDiscovery(result.Discovery),
            Errors: ApiResponseList.Copy(result.Errors),
            FileErrors: ProxyConfigurationFileErrorResponse.FromErrors(result.FileErrors));
    }
}
