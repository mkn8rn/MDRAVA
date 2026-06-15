using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Upstreams;

namespace MDRAVA.BLL.ControlPlane.HealthChecks;

public static class UpstreamHealthCheckTargetMapper
{
    public static IReadOnlyList<UpstreamHealthCheckTarget> FromRoutes(IReadOnlyList<RuntimeRoute> routes)
    {
        return routes
            .Where(static route => route.HealthCheck.Enabled)
            .SelectMany(static route => route.Upstreams.Select(upstream => new UpstreamHealthCheckTarget(
                route.Name,
                upstream.Name,
                upstream.Endpoint,
                upstream.Identity,
                UpstreamTransportEndpointMapper.FromUpstream(upstream),
                route.HealthCheck.Path,
                route.HealthCheck.Interval,
                route.HealthCheck.Timeout,
                route.HealthCheck.HealthyThreshold,
                route.HealthCheck.UnhealthyThreshold)))
            .ToArray();
    }
}
