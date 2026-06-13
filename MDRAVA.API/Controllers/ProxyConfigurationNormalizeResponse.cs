using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.API.Controllers;

public sealed record ProxyConfigurationNormalizeResponse(
    bool Succeeded,
    string Format,
    string? CanonicalJson,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ProxyConfigurationFileError> FileErrors)
{
    public static ProxyConfigurationNormalizeResponse FromResult(ProxyConfigurationNormalizeResult result)
    {
        return result switch
        {
            ProxyConfigurationNormalizeResult.NormalizedResult normalized => new ProxyConfigurationNormalizeResponse(
                Succeeded: true,
                Format: normalized.Format,
                CanonicalJson: normalized.CanonicalJson,
                Errors: normalized.Errors,
                FileErrors: normalized.FileErrors),
            ProxyConfigurationNormalizeResult.FailedResult failed => new ProxyConfigurationNormalizeResponse(
                Succeeded: false,
                Format: failed.Format,
                CanonicalJson: null,
                Errors: failed.Errors,
                FileErrors: failed.FileErrors),
            _ => throw new InvalidOperationException($"Unknown normalize result '{result.GetType().Name}'.")
        };
    }
}
