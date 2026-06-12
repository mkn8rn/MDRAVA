using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Upstreams;

namespace MDRAVA.BLL.ControlPlane.HealthChecks;

public sealed record UpstreamHealthCheckTarget(
    string RouteName,
    string UpstreamName,
    string UpstreamEndpoint,
    string UpstreamIdentity,
    UpstreamTransportEndpoint TransportEndpoint,
    string Path,
    TimeSpan Interval,
    TimeSpan Timeout,
    int HealthyThreshold,
    int UnhealthyThreshold);

public static class UpstreamHealthCheckTargetMapper
{
    public static IReadOnlyList<UpstreamHealthCheckTarget> FromSnapshot(ProxyConfigurationSnapshot snapshot)
    {
        return snapshot.Routes
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
