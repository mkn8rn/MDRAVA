using BusinessProxyConfigurationNormalizeResult =
    MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationNormalizeResult;

namespace MDRAVA.API.Controllers;

public sealed record ProxyConfigurationNormalizeResponse
{
    public ProxyConfigurationNormalizeResponse(
        bool succeeded,
        string format,
        string? canonicalJson,
        IReadOnlyList<string> errors,
        IReadOnlyList<ProxyConfigurationFileErrorResponse> fileErrors)
    {
        Succeeded = succeeded;
        Format = format;
        CanonicalJson = canonicalJson;
        Errors = ApiResponseList.Copy(errors);
        FileErrors = ApiResponseList.Copy(fileErrors);
    }

    public bool Succeeded { get; }

    public string Format { get; }

    public string? CanonicalJson { get; }

    public IReadOnlyList<string> Errors { get; }

    public IReadOnlyList<ProxyConfigurationFileErrorResponse> FileErrors { get; }

    public static ProxyConfigurationNormalizeResponse FromResult(BusinessProxyConfigurationNormalizeResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result switch
        {
            BusinessProxyConfigurationNormalizeResult.NormalizedResult normalized => new ProxyConfigurationNormalizeResponse(
                succeeded: true,
                format: normalized.Format,
                canonicalJson: normalized.CanonicalJson,
                errors: normalized.Errors,
                fileErrors: ProxyConfigurationFileErrorResponse.FromErrors(normalized.FileErrors)),
            BusinessProxyConfigurationNormalizeResult.FailedResult failed => new ProxyConfigurationNormalizeResponse(
                succeeded: false,
                format: failed.Format,
                canonicalJson: null,
                errors: failed.Errors,
                fileErrors: ProxyConfigurationFileErrorResponse.FromErrors(failed.FileErrors)),
            _ => throw new InvalidOperationException($"Unknown normalize result '{result.GetType().Name}'.")
        };
    }
}
