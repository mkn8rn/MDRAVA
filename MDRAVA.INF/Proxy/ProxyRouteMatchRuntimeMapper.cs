using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Http1;
using MDRAVA.BLL.ControlPlane.Routing;

namespace MDRAVA.INF.Proxy;

internal static class ProxyRouteMatchRuntimeMapper
{
    public static IReadOnlyList<RouteMatchCandidate> ToCandidates(IReadOnlyList<RuntimeRoute> routes)
    {
        return routes
            .Select(static route => new RouteMatchCandidate(route.Host, route.PathPrefix))
            .ToArray();
    }

    public static RouteMatchRequest ToRequest(Http1RequestHead requestHead)
    {
        return new RouteMatchRequest(requestHead.Host, requestHead.Path);
    }

    public static RuntimeRoute SelectRoute(IReadOnlyList<RuntimeRoute> routes, RouteMatch match)
    {
        return routes[match.RouteIndex];
    }
}
