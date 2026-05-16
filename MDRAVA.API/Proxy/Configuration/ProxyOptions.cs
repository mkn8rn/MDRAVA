namespace MDRAVA.API.Proxy.Configuration;

public sealed class ProxyOptions
{
    public const string SectionName = "Proxy";

    public List<ListenerOptions> Listeners { get; init; } = [];

    public List<ProxyRouteOptions> Routes { get; init; } = [];
}
