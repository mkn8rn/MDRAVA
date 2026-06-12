namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public interface IProxyConfigurationNormalizeOperations
{
    ProxyConfigurationNormalizeResult Normalize(ProxyConfigurationNormalizeRequest? request);
}
