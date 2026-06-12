namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

using MDRAVA.BLL.ControlPlane.Routing;

public sealed class ProxyRouteDiagnosticsActionPolicyAdapter
    : IProxyRouteDiagnosticsActionPolicy
{
    public ProxyRouteDiagnosticsActionDecision Evaluate(
        IProxyRouteDiagnosticsRoute route,
        ProxyRouteDiagnosticsRequestHead requestHead,
        IProxyRouteDiagnosticsListener listener,
        bool isUpgradeRequest)
    {
        var policyRedirect = isUpgradeRequest
            ? ProxyRoutePolicyRedirectDecision.NoRedirect
            : ProxyRoutePolicyRedirectEvaluator.Evaluate(ToPolicyRedirectInput(route, requestHead, listener));
        if (policyRedirect is ProxyRoutePolicyRedirectDecision.RedirectDecision redirect)
        {
            return ProxyRouteDiagnosticsActionDecision.GeneratedResponse(redirect.StatusCode);
        }

        if (route.Maintenance.Enabled)
        {
            return ProxyRouteDiagnosticsActionDecision.GeneratedResponse(503);
        }

        if (string.Equals(route.Action, "Redirect", StringComparison.OrdinalIgnoreCase))
        {
            return ProxyRouteDiagnosticsActionDecision.GeneratedResponse(route.Redirect.StatusCode);
        }

        if (string.Equals(route.Action, "StaticResponse", StringComparison.OrdinalIgnoreCase))
        {
            return ProxyRouteDiagnosticsActionDecision.GeneratedResponse(route.StaticResponse.StatusCode);
        }

        return ProxyRouteDiagnosticsActionDecision.Proxy;
    }

    private static ProxyRoutePolicyRedirectInput ToPolicyRedirectInput(
        IProxyRouteDiagnosticsRoute route,
        ProxyRouteDiagnosticsRequestHead requestHead,
        IProxyRouteDiagnosticsListener listener)
    {
        return new ProxyRoutePolicyRedirectInput(
            route.HttpsRedirect.Enabled,
            route.HttpsRedirect.StatusCode,
            route.HttpsRedirect.HttpsPort,
            route.CanonicalHost.Enabled,
            route.CanonicalHost.TargetHost,
            route.CanonicalHost.StatusCode,
            listener.Transport,
            requestHead.Host,
            requestHead.Target);
    }
}
