using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public static class ProxyRouteDiagnosticsPolicyExplainer
{
    public static RouteMatchDryRunPolicy Disabled(string reason)
    {
        return new RouteMatchDryRunPolicy(false, false, reason);
    }

    public static RouteMatchDryRunPolicy ExplainCache(
        IProxyRouteDiagnosticsRoute route,
        ProxyRouteDiagnosticsRequestHead requestHead,
        bool wouldProxy)
    {
        if (!route.CacheEnabled)
        {
            return Disabled("disabled");
        }

        if (!wouldProxy)
        {
            return new RouteMatchDryRunPolicy(true, false, "not_proxy_action");
        }

        if (!route.CacheMethods.Any(method => string.Equals(method, requestHead.Method, StringComparison.OrdinalIgnoreCase)))
        {
            return new RouteMatchDryRunPolicy(true, false, "method_not_cacheable");
        }

        if (requestHead.Framing.Kind != ProxyRouteDiagnosticsBodyKind.None)
        {
            return new RouteMatchDryRunPolicy(true, false, "request_body");
        }

        if (ContainsHeader(requestHead.Headers, "Authorization"))
        {
            return new RouteMatchDryRunPolicy(true, false, "authorization");
        }

        return new RouteMatchDryRunPolicy(true, true, "eligible_before_origin_response");
    }

    public static RouteMatchDryRunPolicy ExplainRetry(
        IProxyRouteDiagnosticsRoute route,
        ProxyRouteDiagnosticsRequestHead requestHead,
        bool wouldProxy)
    {
        if (!route.RetryEnabled)
        {
            return Disabled("disabled");
        }

        if (!wouldProxy)
        {
            return new RouteMatchDryRunPolicy(true, false, "not_proxy_action");
        }

        if (!route.RetryMethods.Any(method => string.Equals(method, requestHead.Method, StringComparison.OrdinalIgnoreCase)))
        {
            return new RouteMatchDryRunPolicy(true, false, "method_not_retryable");
        }

        if (requestHead.Framing.Kind != ProxyRouteDiagnosticsBodyKind.None)
        {
            return new RouteMatchDryRunPolicy(true, false, "request_body_not_replayable");
        }

        return new RouteMatchDryRunPolicy(true, true, "eligible_for_configured_transport_or_status_failures");
    }

    public static RouteMatchDryRunPolicy ExplainCircuitBreaker(
        IProxyRouteDiagnosticsRoute route,
        bool wouldProxy)
    {
        var enabled = route.Upstreams.Any(static upstream => upstream.CircuitBreakerEnabled);
        if (!enabled)
        {
            return Disabled("disabled");
        }

        return new RouteMatchDryRunPolicy(
            enabled,
            wouldProxy,
            wouldProxy ? "configured_for_one_or_more_upstreams" : "not_proxy_action");
    }

    private static bool ContainsHeader(IReadOnlyList<ProxyHeaderField> headers, string name)
    {
        return headers.Any(header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
