namespace MDRAVA.BLL.ControlPlane;

public interface IProxyConfigurationReloadMetricsSink
{
    void ConfigReloadSucceeded();

    void ConfigReloadFailed();
}
