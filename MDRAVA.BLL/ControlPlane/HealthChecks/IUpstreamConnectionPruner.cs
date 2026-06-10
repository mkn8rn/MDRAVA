using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.HealthChecks;

public interface IUpstreamConnectionPruner
{
    void PruneIdleConnections(RuntimeUpstream upstream);
}
