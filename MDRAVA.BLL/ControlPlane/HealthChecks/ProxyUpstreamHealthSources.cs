using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.BLL.ControlPlane.Upstreams;

namespace MDRAVA.BLL.ControlPlane.HealthChecks;

public sealed record ProxyUpstreamHealthSource(
    UpstreamHealthStateSource HealthState,
    CircuitBreakerStatusSource CircuitBreaker,
    string Scheme,
    string Protocol,
    int Weight,
    bool ValidateCertificate,
    string? EffectiveSniHost,
    bool HealthCheckEnabled);

public sealed record UpstreamHealthStateSource(
    string UpstreamIdentity,
    string RouteName,
    string UpstreamName,
    string UpstreamEndpoint);

public static class UpstreamHealthStateSourceMapper
{
    public static UpstreamHealthStateSource FromUpstream(RuntimeUpstream upstream)
    {
        return new UpstreamHealthStateSource(
            upstream.Identity,
            upstream.RouteName,
            upstream.Name,
            upstream.Endpoint);
    }
}
