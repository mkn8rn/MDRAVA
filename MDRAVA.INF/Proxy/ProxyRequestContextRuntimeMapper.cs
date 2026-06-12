using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.RequestDiagnostics;

namespace MDRAVA.INF.Proxy;

internal static class ProxyRequestContextRuntimeMapper
{
    public static string ToTransport(RuntimeListener listener)
    {
        return listener.Transport.ToString();
    }

    public static ProxyRequestRoute ToRequestRoute(RuntimeRoute route)
    {
        return new ProxyRequestRoute(
            route.Name,
            route.SiteName,
            route.Maintenance.Enabled ? "maintenance" : ActionName(route.Action),
            route.ResolvedOptions.AccessLogEnabled);
    }

    public static ProxyRequestUpstream ToRequestUpstream(RuntimeUpstream upstream)
    {
        return new ProxyRequestUpstream(
            upstream.Name,
            upstream.Endpoint);
    }

    private static string ActionName(RuntimeRouteAction action)
    {
        return action switch
        {
            RuntimeRouteAction.Redirect => "redirect",
            RuntimeRouteAction.StaticResponse => "staticResponse",
            _ => "proxy"
        };
    }
}
