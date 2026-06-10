namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public interface IProxyConfigurationValidationOperations
{
    ValueTask<ProxyConfigurationValidationResult> ValidateAsync(CancellationToken cancellationToken);
}
