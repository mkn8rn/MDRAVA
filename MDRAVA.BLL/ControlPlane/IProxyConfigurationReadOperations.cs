namespace MDRAVA.BLL.ControlPlane;

public interface IProxyConfigurationReadOperations<TConfiguration>
    where TConfiguration : class
{
    ProxyConfigurationReadResult<TConfiguration> ReadActive();

    ProxyConfigurationReadResult<TConfiguration> ReadEffective();
}
