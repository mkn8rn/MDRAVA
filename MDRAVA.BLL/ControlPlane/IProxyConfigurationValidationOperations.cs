namespace MDRAVA.BLL.ControlPlane;

public interface IProxyConfigurationValidationOperations
{
    ValueTask<ProxyConfigurationValidationResult> ValidateAsync(CancellationToken cancellationToken);
}
