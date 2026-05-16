namespace MDRAVA.API.Proxy.Configuration;

public sealed class ProxyRouteOptions
{
    public string Name { get; init; } = "";

    public string Host { get; init; } = "*";

    public string PathPrefix { get; init; } = "/";

    public List<UpstreamOptions> Upstreams { get; init; } = [];
}
