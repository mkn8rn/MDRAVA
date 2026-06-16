using BusinessProxyConfigurationValidationResult =
    MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationValidationResult;

namespace MDRAVA.API.Controllers;

public sealed record ProxyConfigurationValidationResponse
{
    public ProxyConfigurationValidationResponse(
        bool succeeded,
        string sourceDirectory,
        DateTimeOffset attemptedAtUtc,
        int? activeVersion,
        DateTimeOffset? lastSuccessfulLoadAtUtc,
        int? wouldBeVersion,
        IReadOnlyList<string> sourceFiles,
        ProxyConfigurationDiscoveryResponse discovery,
        IReadOnlyList<string> errors,
        IReadOnlyList<ProxyConfigurationFileErrorResponse> fileErrors)
    {
        ArgumentNullException.ThrowIfNull(discovery);

        Succeeded = succeeded;
        SourceDirectory = sourceDirectory;
        AttemptedAtUtc = attemptedAtUtc;
        ActiveVersion = activeVersion;
        LastSuccessfulLoadAtUtc = lastSuccessfulLoadAtUtc;
        WouldBeVersion = wouldBeVersion;
        SourceFiles = ApiResponseList.Copy(sourceFiles);
        Discovery = discovery;
        Errors = ApiResponseList.Copy(errors);
        FileErrors = ApiResponseList.Copy(fileErrors);
    }

    public bool Succeeded { get; }

    public string SourceDirectory { get; }

    public DateTimeOffset AttemptedAtUtc { get; }

    public int? ActiveVersion { get; }

    public DateTimeOffset? LastSuccessfulLoadAtUtc { get; }

    public int? WouldBeVersion { get; }

    public IReadOnlyList<string> SourceFiles { get; }

    public ProxyConfigurationDiscoveryResponse Discovery { get; }

    public IReadOnlyList<string> Errors { get; }

    public IReadOnlyList<ProxyConfigurationFileErrorResponse> FileErrors { get; }

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
            succeeded: succeeded,
            sourceDirectory: result.SourceDirectory,
            attemptedAtUtc: result.AttemptedAtUtc,
            activeVersion: result.ActiveVersion,
            lastSuccessfulLoadAtUtc: result.LastSuccessfulLoadAtUtc,
            wouldBeVersion: result.WouldBeVersion,
            sourceFiles: result.SourceFiles,
            discovery: ProxyConfigurationDiscoveryResponse.FromDiscovery(result.Discovery),
            errors: result.Errors,
            fileErrors: ProxyConfigurationFileErrorResponse.FromErrors(result.FileErrors));
    }
}
