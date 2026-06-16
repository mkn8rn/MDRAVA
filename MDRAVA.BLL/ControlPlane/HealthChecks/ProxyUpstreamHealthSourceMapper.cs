using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Upstreams;

namespace MDRAVA.BLL.ControlPlane.HealthChecks;

public static class ProxyUpstreamHealthSourceMapper
{
    public static IReadOnlyList<ProxyUpstreamHealthSource> FromRoutes(IEnumerable<RuntimeRoute> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        return HealthCheckList.Copy(routes
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
            );
    }
}
