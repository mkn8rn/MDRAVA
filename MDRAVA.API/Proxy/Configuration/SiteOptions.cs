namespace MDRAVA.API.Proxy.Configuration;

public sealed class SiteOptions
{
    public string Name { get; init; } = "";

    public List<ListenerOptions> Listeners { get; init; } = [];

    public string Host { get; init; } = "";

    public string PathPrefix { get; init; } = "/";

    public string LoadBalancingPolicy { get; init; } = "round-robin";

    public HealthCheckOptions HealthCheck { get; init; } = new();

    public List<UpstreamOptions> Upstreams { get; init; } = [];
}
