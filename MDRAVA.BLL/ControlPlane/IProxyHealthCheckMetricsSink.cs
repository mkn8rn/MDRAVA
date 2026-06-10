namespace MDRAVA.BLL.ControlPlane;

public interface IProxyHealthCheckMetricsSink
{
    void HealthCheckAttempted();

    void HealthCheckSucceeded();

    void HealthCheckFailed();
}
