namespace MDRAVA.BLL.ControlPlane.RuntimeGuards;

public interface IProxyRateLimitMetricsSink
{
    void RequestRateLimited();

    void UpgradeRateLimited();
}
