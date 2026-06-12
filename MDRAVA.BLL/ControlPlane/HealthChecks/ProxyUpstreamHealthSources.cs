using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.HealthChecks;

public sealed record ProxyUpstreamHealthSource(
    RuntimeUpstream Upstream,
    bool HealthCheckEnabled);

public static class ProxyUpstreamHealthSourceMapper
{
    public static IReadOnlyList<ProxyUpstreamHealthSource> FromSnapshot(ProxyConfigurationSnapshot? snapshot)
    {
        return snapshot?.Routes
            .SelectMany(static route => route.Upstreams.Select(upstream => new ProxyUpstreamHealthSource(
                upstream,
                route.HealthCheck.Enabled)))
            .ToArray() ?? [];
    }
}
