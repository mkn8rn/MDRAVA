namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public interface IProxyConfigurationReloadMetricsSink
{
    void ConfigReloadSucceeded();

    void ConfigReloadFailed();
}
