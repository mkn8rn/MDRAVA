using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.HealthChecks;

public interface IUpstreamHealthCheckClient
{
    ValueTask<HealthCheckSample> CheckAsync(
        RuntimeRoute route,
        RuntimeUpstream upstream,
        CancellationToken cancellationToken);
}
