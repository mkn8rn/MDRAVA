using MDRAVA.BLL.ControlPlane;

namespace MDRAVA.BLL.Infrastructure;

public interface IProxyRestoreConfigurationValidator
{
    ValueTask<ProxyRestoreConfigurationValidationResult> ValidateExistingLayoutAsync(CancellationToken cancellationToken);
}

public sealed record ProxyRestoreConfigurationValidationResult(
    bool Succeeded,
    IReadOnlyList<string> Errors,
    IReadOnlyList<ProxyConfigurationFileError> FileErrors,
    int? WouldBeVersion);
