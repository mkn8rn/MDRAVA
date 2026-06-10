namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public interface IProxyConfigurationReadProjectionSource<TConfiguration>
    where TConfiguration : class
{
    TConfiguration? ReadCurrent();
}
