namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

using MDRAVA.BLL.ControlPlane.Routing;

public sealed class ProxyRouteDiagnosticsMatcher
    : IProxyRouteDiagnosticsMatcher
{
    private readonly SingleUpstreamRouteMatcher _matcher = new();

    public IProxyRouteDiagnosticsRoute? Match(
        IReadOnlyList<IProxyRouteDiagnosticsRoute> routes,
        ProxyRouteDiagnosticsRequestHead requestHead)
    {
        var match = _matcher.Match(
            routes
                .Select(static route => new RouteMatchCandidate(route.Host, route.PathPrefix))
                .ToArray(),
            new RouteMatchRequest(requestHead.Host, requestHead.Path));

        return match is null ? null : routes[match.RouteIndex];
    }
}
