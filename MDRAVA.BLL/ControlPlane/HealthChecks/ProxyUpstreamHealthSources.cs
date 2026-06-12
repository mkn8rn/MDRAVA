using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.HealthChecks;

public sealed record ProxyUpstreamHealthSource(
    RuntimeUpstream Upstream,
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
    public static IReadOnlyList<ProxyUpstreamHealthSource> FromSnapshot(ProxyConfigurationSnapshot? snapshot)
    {
        return snapshot?.Routes
            .SelectMany(static route => route.Upstreams.Select(upstream => new ProxyUpstreamHealthSource(
                upstream,
                route.HealthCheck.Enabled)))
            .ToArray() ?? [];
    }
}
