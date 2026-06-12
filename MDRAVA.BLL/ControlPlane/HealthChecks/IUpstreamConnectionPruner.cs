using MDRAVA.BLL.ControlPlane.Upstreams;

namespace MDRAVA.BLL.ControlPlane.HealthChecks;

public interface IUpstreamConnectionPruner
{
    void PruneIdleConnections(UpstreamTransportEndpoint endpoint);
}
