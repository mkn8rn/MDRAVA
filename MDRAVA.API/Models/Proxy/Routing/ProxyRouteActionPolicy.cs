using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using MDRAVA.API.Proxy.Forwarding;
using MDRAVA.API.Proxy.Protocol;

namespace MDRAVA.API.Proxy.Routing;

public sealed class ProxyRouteActionPolicy
{
    public RouteActionDecision Evaluate(
        RuntimeRoute route,
        Http1RequestHead requestHead,
        RuntimeListener listener,
        bool isUpgradeRequest)
    {
        if (!isUpgradeRequest && TryBuildPolicyRedirect(route, requestHead, listener, out var policyRedirect))
        {
            return new RouteActionDecision(policyRedirect);
        }

        if (route.Maintenance.Enabled)
        {
            List<Http1HeaderField> headers = [];
            if (route.Maintenance.RetryAfterSeconds.HasValue)
            {
                headers.Add(new Http1HeaderField("Retry-After", route.Maintenance.RetryAfterSeconds.Value.ToString(CultureInfo.InvariantCulture)));
            }

            return new RouteActionDecision(new GeneratedRouteResponse(
                503,
                "Service Unavailable",
                route.Maintenance.ContentType,
                route.Maintenance.Body,
                headers));
        }

        return route.Action switch
        {
            RuntimeRouteAction.Redirect => new RouteActionDecision(BuildRouteRedirect(route, requestHead)),
            RuntimeRouteAction.StaticResponse => new RouteActionDecision(new GeneratedRouteResponse(
                route.StaticResponse.StatusCode,
                ReasonPhrase(route.StaticResponse.StatusCode),
                route.StaticResponse.ContentType,
                route.StaticResponse.Body,
                [])),
            _ => RouteActionDecision.Proxy
        };
    }

    private static bool TryBuildPolicyRedirect(
        RuntimeRoute route,
        Http1RequestHead requestHead,
        RuntimeListener listener,
        [NotNullWhen(true)] out GeneratedRouteResponse? response)
    {
        response = null;
        var scheme = listener.Transport == RuntimeListenerTransport.Https ? "https" : "http";
        var host = requestHead.Host;
        var statusCode = 308;
        var shouldRedirect = false;

        if (route.HttpsRedirect.Enabled && listener.Transport == RuntimeListenerTransport.Http)
        {
            scheme = "https";
            statusCode = route.HttpsRedirect.StatusCode;
            shouldRedirect = true;
        }

        if (route.CanonicalHost.Enabled
            && !string.IsNullOrWhiteSpace(route.CanonicalHost.TargetHost)
            && !HostEquals(host, route.CanonicalHost.TargetHost))
        {
            host = route.CanonicalHost.TargetHost;
            statusCode = route.CanonicalHost.StatusCode;
            shouldRedirect = true;
        }

        if (!shouldRedirect)
        {
            return false;
        }

        if (scheme == "https" && route.HttpsRedirect.HttpsPort.HasValue)
        {
            host = ApplyPort(host, route.HttpsRedirect.HttpsPort.Value, defaultPort: 443);
        }

        var location = $"{scheme}://{host}{requestHead.Target}";
        response = RedirectResponse(statusCode, location);
        return true;
    }

    private static GeneratedRouteResponse BuildRouteRedirect(RuntimeRoute route, Http1RequestHead requestHead)
    {
        var location = !string.IsNullOrWhiteSpace(route.Redirect.TargetUrl)
            ? route.Redirect.TargetUrl
            : route.Redirect.TargetPath;

        if (route.Redirect.PreserveQuery)
        {
            var query = ExtractQuery(requestHead.Target);
            if (!string.IsNullOrEmpty(query) && !location.Contains('?'))
            {
                location += query;
            }
        }

        return RedirectResponse(route.Redirect.StatusCode, location);
    }

    private static GeneratedRouteResponse RedirectResponse(int statusCode, string location)
    {
        return new GeneratedRouteResponse(
            statusCode,
            ReasonPhrase(statusCode),
            null,
            "",
            [new Http1HeaderField("Location", location)]);
    }

    private static bool HostEquals(string requestHost, string targetHost)
    {
        return string.Equals(StripSimplePort(requestHost), StripSimplePort(targetHost), StringComparison.OrdinalIgnoreCase);
    }

    private static string ApplyPort(string host, int port, int defaultPort)
    {
        var hostWithoutPort = StripSimplePort(host);
        return port == defaultPort
            ? hostWithoutPort
            : $"{hostWithoutPort}:{port}";
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

    private static string ExtractQuery(string target)
    {
        var queryIndex = target.IndexOf('?');
        return queryIndex < 0 ? "" : target[queryIndex..];
    }

    public static string ReasonPhrase(int statusCode)
    {
        return statusCode switch
        {
            200 => "OK",
            201 => "Created",
            202 => "Accepted",
            204 => "No Content",
            301 => "Moved Permanently",
            302 => "Found",
            303 => "See Other",
            307 => "Temporary Redirect",
            308 => "Permanent Redirect",
            400 => "Bad Request",
            404 => "Not Found",
            410 => "Gone",
            429 => "Too Many Requests",
            500 => "Internal Server Error",
            502 => "Bad Gateway",
            503 => "Service Unavailable",
            504 => "Gateway Timeout",
            _ => "Status"
        };
    }
}
