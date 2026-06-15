using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Routing;

public static class ProxyRouteActionKindMapper
{
    public static ProxyRouteActionKind FromRuntimeAction(RuntimeRouteAction action)
    {
        return action switch
        {
            RuntimeRouteAction.Redirect => ProxyRouteActionKind.Redirect,
            RuntimeRouteAction.StaticResponse => ProxyRouteActionKind.StaticResponse,
            _ => ProxyRouteActionKind.Proxy
        };
    }

    public static ProxyRouteActionKind FromText(string action)
    {
        return action switch
        {
            _ when string.Equals(action, "Redirect", StringComparison.OrdinalIgnoreCase) =>
                ProxyRouteActionKind.Redirect,
            _ when string.Equals(action, "StaticResponse", StringComparison.OrdinalIgnoreCase) =>
                ProxyRouteActionKind.StaticResponse,
            _ => ProxyRouteActionKind.Proxy
        };
    }
}
