using MDRAVA.BLL.Http;
using MDRAVA.BLL.ControlPlane.Headers;
using MDRAVA.BLL.ControlPlane.Http1;
using System.Globalization;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Routing;

public sealed class ProxyRouteActionPolicy
{
    public RouteActionDecision Evaluate(
        RuntimeRoute route,
        Http1RequestHead requestHead,
        RuntimeListener listener,
        bool isUpgradeRequest)
    {
        var policyRedirect = isUpgradeRequest
            ? ProxyRoutePolicyRedirectDecision.NoRedirect
            : ProxyRoutePolicyRedirectEvaluator.Evaluate(ToPolicyRedirectInput(route, requestHead, listener));
        if (policyRedirect is ProxyRoutePolicyRedirectDecision.RedirectDecision redirect)
        {
            return new RouteActionDecision(RedirectResponse(redirect.StatusCode, redirect.Location));
        }

        if (route.Maintenance.Enabled)
        {
            List<ProxyHeaderField> headers = [];
            if (route.Maintenance.RetryAfterSeconds.HasValue)
            {
                headers.Add(new ProxyHeaderField("Retry-After", route.Maintenance.RetryAfterSeconds.Value.ToString(CultureInfo.InvariantCulture)));
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

    private static ProxyRoutePolicyRedirectInput ToPolicyRedirectInput(
        RuntimeRoute route,
        Http1RequestHead requestHead,
        RuntimeListener listener)
    {
        return new ProxyRoutePolicyRedirectInput(
            route.HttpsRedirect.Enabled,
            route.HttpsRedirect.StatusCode,
            route.HttpsRedirect.HttpsPort,
            route.CanonicalHost.Enabled,
            route.CanonicalHost.TargetHost,
            route.CanonicalHost.StatusCode,
            listener.Transport == RuntimeListenerTransport.Https ? "https" : "http",
            requestHead.Host,
            requestHead.Target);
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
            [new ProxyHeaderField("Location", location)]);
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
