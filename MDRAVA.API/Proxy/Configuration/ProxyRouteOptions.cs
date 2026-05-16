namespace MDRAVA.API.Proxy.Configuration;

public sealed class ProxyRouteOptions
{
    public string Name { get; init; } = "";

    public string Host { get; init; } = "*";

    public string PathPrefix { get; init; } = "/";

    public string LoadBalancingPolicy { get; init; } = "round-robin";

    public HealthCheckOptions HealthCheck { get; init; } = new();

    public List<UpstreamOptions> Upstreams { get; init; } = [];
}
