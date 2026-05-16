using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Protocol;

namespace MDRAVA.API.Proxy.Routing;

public sealed class SingleUpstreamRouteMatcher : IRouteMatcher
{
    public RouteMatch? Match(ProxyConfigurationSnapshot snapshot, Http1RequestHead requestHead)
    {
        foreach (var route in snapshot.Routes)
        {
            if (!HostMatches(route.Host, requestHead.Host))
            {
                continue;
            }

            if (!requestHead.Path.StartsWith(route.PathPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var upstream = route.Upstreams.Count == 0 ? null : route.Upstreams[0];
            if (upstream is null)
            {
                continue;
            }

            return new RouteMatch(route, upstream);
        }

        return null;
    }

    private static bool HostMatches(string configuredHost, string requestHost)
    {
        if (configuredHost == "*")
        {
            return true;
        }

        if (string.Equals(configuredHost, requestHost, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var requestHostWithoutPort = StripSimplePort(requestHost);
        return string.Equals(configuredHost, requestHostWithoutPort, StringComparison.OrdinalIgnoreCase);
    }

    private static string StripSimplePort(string host)
    {
        var colonIndex = host.LastIndexOf(':');
        if (colonIndex <= 0 || host.Contains(']'))
        {
            return host;
        }

        return host[..colonIndex];
    }
}
