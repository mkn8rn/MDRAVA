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
