using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.HealthChecks;

public interface IProxyUpstreamHealthMetricsSink
{
    void UpstreamHealthTransition();

    void UpstreamRequestFailed(RuntimeUpstream upstream);
}
