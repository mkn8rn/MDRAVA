namespace MDRAVA.API.Models.Configuration;

public sealed class ProxyOptions
{
    public const string SectionName = "Proxy";

    public List<ListenerOptions> Listeners { get; init; } = [];

    public List<ProxyRouteOptions> Routes { get; init; } = [];
}
