using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.API.Controllers;

public sealed record ProxyConfigurationValidationResponse(
    bool Succeeded,
    string SourceDirectory,
    DateTimeOffset AttemptedAtUtc,
    int? ActiveVersion,
    DateTimeOffset? LastSuccessfulLoadAtUtc,
    int? WouldBeVersion,
    IReadOnlyList<string> SourceFiles,
    ProxyConfigurationDiscovery Discovery,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ProxyConfigurationFileError> FileErrors)
{
    public static ProxyConfigurationValidationResponse FromResult(ProxyConfigurationValidationResult result)
    {
        return result switch
        {
            ProxyConfigurationValidationResult.ValidResult valid => FromResult(valid, succeeded: true),
            ProxyConfigurationValidationResult.InvalidResult invalid => FromResult(invalid, succeeded: false),
            _ => throw new InvalidOperationException($"Unknown validation result '{result.GetType().Name}'.")
        };
    }

    private static ProxyConfigurationValidationResponse FromResult(
        ProxyConfigurationValidationResult result,
        bool succeeded)
    {
        return new ProxyConfigurationValidationResponse(
            Succeeded: succeeded,
            SourceDirectory: result.SourceDirectory,
            AttemptedAtUtc: result.AttemptedAtUtc,
            ActiveVersion: result.ActiveVersion,
            LastSuccessfulLoadAtUtc: result.LastSuccessfulLoadAtUtc,
            WouldBeVersion: result.WouldBeVersion,
            SourceFiles: result.SourceFiles,
            Discovery: result.Discovery,
            Errors: result.Errors,
            FileErrors: result.FileErrors);
    }
}
