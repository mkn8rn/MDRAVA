namespace MDRAVA.BLL.ControlPlane.HealthChecks;

public interface IUpstreamHealthCheckClient
{
    ValueTask<HealthCheckSample> CheckAsync(
        UpstreamHealthCheckTarget target,
        CancellationToken cancellationToken);
}
