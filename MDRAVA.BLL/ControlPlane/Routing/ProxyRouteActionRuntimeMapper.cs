using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Http1;

namespace MDRAVA.BLL.ControlPlane.Routing;

public static class ProxyRouteActionRuntimeMapper
{
    public static ProxyRouteActionInput ToPolicyInput(
        RuntimeRoute route,
        Http1RequestHead requestHead,
        RuntimeListener listener,
        bool isUpgradeRequest)
    {
        return new ProxyRouteActionInput(
            ToActionKind(route.Action),
            new ProxyRoutePolicyRedirectInput(
                route.HttpsRedirect.Enabled,
                route.HttpsRedirect.StatusCode,
                route.HttpsRedirect.HttpsPort,
                route.CanonicalHost.Enabled,
                route.CanonicalHost.TargetHost,
                route.CanonicalHost.StatusCode,
                listener.Transport == RuntimeListenerTransport.Https ? "https" : "http",
                requestHead.Host,
                requestHead.Target),
            new ProxyRouteMaintenanceActionInput(
                route.Maintenance.Enabled,
                route.Maintenance.RetryAfterSeconds,
                route.Maintenance.ContentType,
                route.Maintenance.Body),
            new ProxyRouteRedirectActionInput(
                route.Redirect.StatusCode,
                route.Redirect.TargetUrl,
                route.Redirect.TargetPath,
                route.Redirect.PreserveQuery),
            new ProxyRouteStaticResponseActionInput(
                route.StaticResponse.StatusCode,
                route.StaticResponse.ContentType,
                route.StaticResponse.Body),
            isUpgradeRequest);
    }

    private static ProxyRouteActionKind ToActionKind(RuntimeRouteAction action)
    {
        return action switch
        {
            RuntimeRouteAction.Redirect => ProxyRouteActionKind.Redirect,
            RuntimeRouteAction.StaticResponse => ProxyRouteActionKind.StaticResponse,
            _ => ProxyRouteActionKind.Proxy
        };
    }
}
