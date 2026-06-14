namespace MDRAVA.BLL.ControlPlane.HealthChecks;

public interface IUpstreamHealthCheckTargetSource
{
    IReadOnlyList<UpstreamHealthCheckTarget> ReadTargets();
}
