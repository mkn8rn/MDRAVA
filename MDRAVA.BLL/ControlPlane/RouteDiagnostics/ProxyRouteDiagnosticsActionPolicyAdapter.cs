namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed class ProxyRouteDiagnosticsActionPolicyAdapter
    : IProxyRouteDiagnosticsActionPolicy
{
    public ProxyRouteDiagnosticsActionDecision Evaluate(
        IProxyRouteDiagnosticsRoute route,
        ProxyRouteDiagnosticsRequestHead requestHead,
        IProxyRouteDiagnosticsListener listener,
        bool isUpgradeRequest)
    {
        if (!isUpgradeRequest && TryBuildPolicyRedirect(route, requestHead, listener, out var policyRedirectStatusCode))
        {
            return new ProxyRouteDiagnosticsActionDecision(false, policyRedirectStatusCode);
        }

        if (route.Maintenance.Enabled)
        {
            return new ProxyRouteDiagnosticsActionDecision(false, 503);
        }

        if (string.Equals(route.Action, "Redirect", StringComparison.OrdinalIgnoreCase))
        {
            return new ProxyRouteDiagnosticsActionDecision(false, route.Redirect.StatusCode);
        }

        if (string.Equals(route.Action, "StaticResponse", StringComparison.OrdinalIgnoreCase))
        {
            return new ProxyRouteDiagnosticsActionDecision(false, route.StaticResponse.StatusCode);
        }

        return new ProxyRouteDiagnosticsActionDecision(true, null);
    }

    private static bool TryBuildPolicyRedirect(
        IProxyRouteDiagnosticsRoute route,
        ProxyRouteDiagnosticsRequestHead requestHead,
        IProxyRouteDiagnosticsListener listener,
        out int statusCode)
    {
        statusCode = 308;
        var shouldRedirect = false;

        if (route.HttpsRedirect.Enabled && string.Equals(listener.Transport, "http", StringComparison.OrdinalIgnoreCase))
        {
            statusCode = route.HttpsRedirect.StatusCode;
            shouldRedirect = true;
        }

        if (route.CanonicalHost.Enabled
            && !string.IsNullOrWhiteSpace(route.CanonicalHost.TargetHost)
            && !HostEquals(requestHead.Host, route.CanonicalHost.TargetHost))
        {
            statusCode = route.CanonicalHost.StatusCode;
            shouldRedirect = true;
        }

        return shouldRedirect;
    }

    private static bool HostEquals(string requestHost, string targetHost)
    {
        return string.Equals(StripSimplePort(requestHost), StripSimplePort(targetHost), StringComparison.OrdinalIgnoreCase);
    }

    private static string StripSimplePort(string host)
    {
        var colonIndex = host.LastIndexOf(':');
        if (colonIndex <= 0 || host.Contains(']', StringComparison.Ordinal))
        {
            return host;
        }

        return host[..colonIndex];
    }
}
