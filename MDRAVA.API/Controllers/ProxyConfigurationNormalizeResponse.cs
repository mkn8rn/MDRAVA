using BusinessProxyConfigurationNormalizeResult =
    MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationNormalizeResult;

namespace MDRAVA.API.Controllers;

public sealed record ProxyConfigurationNormalizeResponse(
    bool Succeeded,
    string Format,
    string? CanonicalJson,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ProxyConfigurationFileErrorResponse> FileErrors)
{
    public static ProxyConfigurationNormalizeResponse FromResult(BusinessProxyConfigurationNormalizeResult result)
    {
        return result switch
        {
            BusinessProxyConfigurationNormalizeResult.NormalizedResult normalized => new ProxyConfigurationNormalizeResponse(
                Succeeded: true,
                Format: normalized.Format,
                CanonicalJson: normalized.CanonicalJson,
                Errors: normalized.Errors.ToArray(),
                FileErrors: ProxyConfigurationFileErrorResponse.FromErrors(normalized.FileErrors)),
            BusinessProxyConfigurationNormalizeResult.FailedResult failed => new ProxyConfigurationNormalizeResponse(
                Succeeded: false,
                Format: failed.Format,
                CanonicalJson: null,
                Errors: failed.Errors.ToArray(),
                FileErrors: ProxyConfigurationFileErrorResponse.FromErrors(failed.FileErrors)),
            _ => throw new InvalidOperationException($"Unknown normalize result '{result.GetType().Name}'.")
        };
    }
}
