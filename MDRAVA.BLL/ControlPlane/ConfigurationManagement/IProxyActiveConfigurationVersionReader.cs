namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public interface IProxyActiveConfigurationVersionReader
{
    int? ActiveConfigVersion { get; }
}
