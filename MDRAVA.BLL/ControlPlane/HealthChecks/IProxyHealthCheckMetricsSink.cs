namespace MDRAVA.BLL.ControlPlane.HealthChecks;

public interface IProxyHealthCheckMetricsSink
{
    void HealthCheckAttempted();

    void HealthCheckSucceeded();

    void HealthCheckFailed();
}
