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

public static class ProxyUpstreamHealthSourceMapper
{
    public static IReadOnlyList<ProxyUpstreamHealthSource> FromRoutes(IReadOnlyList<RuntimeRoute> routes)
    {
        return routes
            .SelectMany(static route => route.Upstreams.Select(upstream => new ProxyUpstreamHealthSource(
                UpstreamHealthStateSourceMapper.FromUpstream(upstream),
                CircuitBreakerStatusSourceMapper.FromUpstream(upstream),
                upstream.Scheme,
                upstream.Protocol,
                upstream.Weight,
                string.Equals(upstream.Scheme, "https", StringComparison.OrdinalIgnoreCase)
                    && upstream.Tls.ValidateCertificate,
                string.Equals(upstream.Scheme, "https", StringComparison.OrdinalIgnoreCase)
                    ? upstream.EffectiveSniHost
                    : null,
                route.HealthCheck.Enabled)))
            .ToArray();
    }
}
