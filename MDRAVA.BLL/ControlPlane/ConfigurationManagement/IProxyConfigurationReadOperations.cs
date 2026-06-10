namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public interface IProxyConfigurationReadOperations<TConfiguration>
    where TConfiguration : class
{
    ProxyConfigurationReadResult<TConfiguration> ReadActive();

    ProxyConfigurationReadResult<TConfiguration> ReadEffective();
}
