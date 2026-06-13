using System.Text;
using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.Configuration;

public static partial class ProxyOptionsValidationRules
{
    private const int MaxGeneratedBodyBytes = 64 * 1024;
    private const long MaximumCacheEntryBytes = 64L * 1024 * 1024;
    private const long MaximumCacheTotalBytes = 512L * 1024 * 1024;
    private static readonly HashSet<int> RedirectStatusCodes = [301, 302, 303, 307, 308];

    public static IReadOnlyList<string> Validate(
        ProxyOptions options,
        IProxyEndpointAddressPolicy endpointAddressPolicy,
        IProxyUrlSyntaxPolicy urlSyntaxPolicy)
    {
        List<string> failures = [];

        ValidateListeners(failures, options, endpointAddressPolicy);

        if (options.Routes.Count == 0)
        {
            failures.Add("Proxy:Routes must contain at least one route.");
        }

        HashSet<string> routeNames = new(StringComparer.OrdinalIgnoreCase);
        for (var routeIndex = 0; routeIndex < options.Routes.Count; routeIndex++)
        {
            var route = options.Routes[routeIndex];
            var routePrefix = $"Proxy:Routes:{routeIndex}";

            if (string.IsNullOrWhiteSpace(route.Name))
            {
                failures.Add($"{routePrefix}:Name is required.");
            }
            else if (!routeNames.Add(route.Name))
            {
                failures.Add($"{routePrefix}:Name '{route.Name}' is duplicated.");
            }

            if (string.IsNullOrWhiteSpace(route.Host))
            {
                failures.Add($"{routePrefix}:Host is required.");
            }

            if (string.IsNullOrWhiteSpace(route.PathPrefix) || !route.PathPrefix.StartsWith('/'))
            {
                failures.Add($"{routePrefix}:PathPrefix must start with '/'.");
            }

            var routeAction = string.IsNullOrWhiteSpace(route.Action) ? "proxy" : route.Action;
            if (!IsRouteAction(routeAction))
            {
                failures.Add($"{routePrefix}:Action must be 'proxy', 'redirect', or 'staticResponse'.");
            }

            if (IsProxyAction(routeAction)
                && !string.Equals(route.LoadBalancingPolicy, "round-robin", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"{routePrefix}:LoadBalancingPolicy must be 'round-robin' for Phase 8.");
            }

            if (IsProxyAction(routeAction))
            {
                ValidateHealthCheck(failures, routePrefix, route.HealthCheck);
            }

            ValidateRedirectPolicy(failures, routePrefix, route.HttpsRedirect);
            ValidateCanonicalHost(failures, routePrefix, route.CanonicalHost);
            ProxyHeaderPolicyOptionsValidationRules.Validate(failures, routePrefix, route.HeaderPolicy);
            ValidatePathRewrite(failures, routePrefix, route.PathRewrite);
            ValidateMaintenance(failures, routePrefix, route.Maintenance);
            ValidateCachePolicy(failures, routePrefix, route.Cache, routeAction);
            ValidateRetryPolicy(failures, routePrefix, route.Retry, routeAction);
            ValidateOverrides(failures, routePrefix, route.Overrides);

            if (IsProxyAction(routeAction) && route.Upstreams.Count == 0)
            {
                failures.Add($"{routePrefix}:Upstreams must contain at least one upstream.");
            }

            if (IsRedirectAction(routeAction))
            {
                ValidateRedirectRoute(failures, routePrefix, route.Redirect, urlSyntaxPolicy);
            }

            if (IsStaticResponseAction(routeAction))
            {
                ValidateStaticResponse(failures, routePrefix, route.StaticResponse);
            }

            HashSet<string> upstreamNames = new(StringComparer.OrdinalIgnoreCase);

            for (var upstreamIndex = 0; upstreamIndex < route.Upstreams.Count; upstreamIndex++)
            {
                var upstream = route.Upstreams[upstreamIndex];
                var upstreamPrefix = $"{routePrefix}:Upstreams:{upstreamIndex}";

                if (string.IsNullOrWhiteSpace(upstream.Name))
                {
                    failures.Add($"{upstreamPrefix}:Name is required.");
                }
                else if (!upstreamNames.Add(upstream.Name))
                {
                    failures.Add($"{upstreamPrefix}:Name '{upstream.Name}' is duplicated within route '{route.Name}'.");
                }

                if (string.IsNullOrWhiteSpace(upstream.Address))
                {
                    failures.Add($"{upstreamPrefix}:Address is required.");
                }
                else if (endpointAddressPolicy.IsAmbiguousUpstreamAddress(upstream.Address))
                {
                    failures.Add($"{upstreamPrefix}:Address must be a host name or IP literal without scheme, path, whitespace, or embedded port.");
                }

                var upstreamScheme = string.IsNullOrWhiteSpace(upstream.Scheme) ? "http" : upstream.Scheme;
                if (!IsSupportedUpstreamScheme(upstreamScheme))
                {
                    failures.Add($"{upstreamPrefix}:Scheme must be 'http' or 'https'.");
                }

                var upstreamProtocol = string.IsNullOrWhiteSpace(upstream.Protocol) ? RuntimeUpstreamProtocol.Http1 : upstream.Protocol;
                if (!IsSupportedUpstreamProtocol(upstreamProtocol))
                {
                    failures.Add($"{upstreamPrefix}:Protocol must be 'http1', 'http2', or 'http3'.");
                }
                else if (string.Equals(upstreamProtocol, RuntimeUpstreamProtocol.Http2, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(upstreamScheme, "https", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"{upstreamPrefix}:HTTP/2 upstreams require scheme 'https' with ALPN; h2c is not supported.");
                }
                else if (string.Equals(upstreamProtocol, RuntimeUpstreamProtocol.Http3, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(upstreamScheme, "https", StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"{upstreamPrefix}:HTTP/3 upstreams require scheme 'https' with QUIC and ALPN; h3c is not supported.");
                }

                if (upstream.Port is < 1 or > 65535)
                {
                    failures.Add($"{upstreamPrefix}:Port must be between 1 and 65535.");
                }

                if (upstream.Weight is < 1 or > 100_000)
                {
                    failures.Add($"{upstreamPrefix}:Weight must be between 1 and 100000.");
                }

                ValidateUpstreamTls(failures, upstreamPrefix, upstream.UpstreamTls, endpointAddressPolicy);
                ValidateCircuitBreaker(failures, upstreamPrefix, upstream.CircuitBreaker);
            }
        }

        return failures;
    }

    private static bool IsRouteAction(string action)
    {
        return IsProxyAction(action) || IsRedirectAction(action) || IsStaticResponseAction(action);
    }

    private static bool IsProxyAction(string action)
    {
        return string.Equals(action, "proxy", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRedirectAction(string action)
    {
        return string.Equals(action, "redirect", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStaticResponseAction(string action)
    {
        return string.Equals(action, "staticResponse", StringComparison.OrdinalIgnoreCase);
    }

    private static string SupportedListenerProtocolsText()
    {
        return string.Join(
            ", ",
            RuntimeListenerProtocolExtensions.SupportedConfigValues.Select(static value => $"'{value}'"));
    }

    private static string SupportedHttp3EnablementsText()
    {
        return string.Join(
            ", ",
            RuntimeHttp3Compatibility.SupportedEnablementConfigValues.Select(static value => $"'{value}'"));
    }

    private static bool IsSupportedUpstreamScheme(string scheme)
    {
        return string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase)
            || string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSupportedUpstreamProtocol(string protocol)
    {
        return string.Equals(protocol, RuntimeUpstreamProtocol.Http1, StringComparison.OrdinalIgnoreCase)
            || string.Equals(protocol, RuntimeUpstreamProtocol.Http2, StringComparison.OrdinalIgnoreCase)
            || string.Equals(protocol, RuntimeUpstreamProtocol.Http3, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateUpstreamTls(
        List<string> failures,
        string upstreamPrefix,
        UpstreamTlsOptions tls,
        IProxyEndpointAddressPolicy endpointAddressPolicy)
    {
        if (string.IsNullOrWhiteSpace(tls.SniHost))
        {
            return;
        }

        if (!endpointAddressPolicy.IsValidSniHost(tls.SniHost))
        {
            failures.Add($"{upstreamPrefix}:UpstreamTls:SniHost must be a DNS host name or IP literal without scheme, path, port, whitespace, or wildcard.");
        }
    }

    private static void ValidateHealthCheck(
        List<string> failures,
        string routePrefix,
        HealthCheckOptions healthCheck)
    {
        var prefix = $"{routePrefix}:HealthCheck";

        if (string.IsNullOrWhiteSpace(healthCheck.Path) || !healthCheck.Path.StartsWith('/'))
        {
            failures.Add($"{prefix}:Path must start with '/'.");
        }

        if (healthCheck.IntervalSeconds is < 1 or > 3600)
        {
            failures.Add($"{prefix}:IntervalSeconds must be between 1 and 3600.");
        }

        if (healthCheck.TimeoutSeconds is < 1 or > 300)
        {
            failures.Add($"{prefix}:TimeoutSeconds must be between 1 and 300.");
        }

        if (healthCheck.TimeoutSeconds > healthCheck.IntervalSeconds)
        {
            failures.Add($"{prefix}:TimeoutSeconds must not exceed IntervalSeconds.");
        }

        if (healthCheck.HealthyThreshold is < 1 or > 100)
        {
            failures.Add($"{prefix}:HealthyThreshold must be between 1 and 100.");
        }

        if (healthCheck.UnhealthyThreshold is < 1 or > 100)
        {
            failures.Add($"{prefix}:UnhealthyThreshold must be between 1 and 100.");
        }
    }

    private static void ValidateRedirectPolicy(
        List<string> failures,
        string routePrefix,
        ProxyHttpsRedirectOptions redirect)
    {
        if (redirect.StatusCode.HasValue && !RedirectStatusCodes.Contains(redirect.StatusCode.Value))
        {
            failures.Add($"{routePrefix}:HttpsRedirect:StatusCode must be one of 301, 302, 303, 307, or 308.");
        }

        if (redirect.HttpsPort is < 1 or > 65535)
        {
            failures.Add($"{routePrefix}:HttpsRedirect:HttpsPort must be between 1 and 65535.");
        }
    }

    private static void ValidateCanonicalHost(
        List<string> failures,
        string routePrefix,
        ProxyCanonicalHostOptions canonicalHost)
    {
        if (canonicalHost.StatusCode.HasValue && !RedirectStatusCodes.Contains(canonicalHost.StatusCode.Value))
        {
            failures.Add($"{routePrefix}:CanonicalHost:StatusCode must be one of 301, 302, 303, 307, or 308.");
        }

        var enabled = canonicalHost.Enabled == true || !string.IsNullOrWhiteSpace(canonicalHost.TargetHost);
        if (!enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(canonicalHost.TargetHost))
        {
            failures.Add($"{routePrefix}:CanonicalHost:TargetHost is required when canonical host redirect is enabled.");
            return;
        }

        if (canonicalHost.TargetHost.Contains('/', StringComparison.Ordinal)
            || canonicalHost.TargetHost.Contains('\\', StringComparison.Ordinal)
            || canonicalHost.TargetHost.Any(char.IsWhiteSpace))
        {
            failures.Add($"{routePrefix}:CanonicalHost:TargetHost must be a host name, not a URL or path.");
        }
    }

    private static void ValidatePathRewrite(
        List<string> failures,
        string routePrefix,
        ProxyPathRewriteOptions rewrite)
    {
        if (!string.IsNullOrWhiteSpace(rewrite.StripPrefix) && !rewrite.StripPrefix.StartsWith('/'))
        {
            failures.Add($"{routePrefix}:PathRewrite:StripPrefix must start with '/'.");
        }

        if (!string.IsNullOrWhiteSpace(rewrite.ReplacePrefix) && !rewrite.ReplacePrefix.StartsWith('/'))
        {
            failures.Add($"{routePrefix}:PathRewrite:ReplacePrefix must start with '/'.");
        }

        if (!string.IsNullOrWhiteSpace(rewrite.Replacement) && !rewrite.Replacement.StartsWith('/'))
        {
            failures.Add($"{routePrefix}:PathRewrite:Replacement must start with '/'.");
        }

        if (!string.IsNullOrWhiteSpace(rewrite.StripPrefix)
            && !string.IsNullOrWhiteSpace(rewrite.ReplacePrefix))
        {
            failures.Add($"{routePrefix}:PathRewrite must not configure both StripPrefix and ReplacePrefix.");
        }

        if (!string.IsNullOrWhiteSpace(rewrite.ReplacePrefix)
            && string.IsNullOrWhiteSpace(rewrite.Replacement))
        {
            failures.Add($"{routePrefix}:PathRewrite:Replacement is required when ReplacePrefix is configured.");
        }
    }

    private static void ValidateRedirectRoute(
        List<string> failures,
        string routePrefix,
        ProxyRedirectOptions redirect,
        IProxyUrlSyntaxPolicy urlSyntaxPolicy)
    {
        var statusCode = redirect.StatusCode ?? 308;
        if (!RedirectStatusCodes.Contains(statusCode))
        {
            failures.Add($"{routePrefix}:Redirect:StatusCode must be one of 301, 302, 303, 307, or 308.");
        }

        var hasTargetUrl = !string.IsNullOrWhiteSpace(redirect.TargetUrl);
        var hasTargetPath = !string.IsNullOrWhiteSpace(redirect.TargetPath);
        if (hasTargetUrl == hasTargetPath)
        {
            failures.Add($"{routePrefix}:Redirect must set exactly one of TargetUrl or TargetPath.");
            return;
        }

        if (hasTargetUrl && !urlSyntaxPolicy.IsAbsoluteUrl(redirect.TargetUrl))
        {
            failures.Add($"{routePrefix}:Redirect:TargetUrl must be an absolute URL.");
        }

        if (hasTargetPath && !redirect.TargetPath.StartsWith('/'))
        {
            failures.Add($"{routePrefix}:Redirect:TargetPath must start with '/'.");
        }
    }

    private static void ValidateStaticResponse(
        List<string> failures,
        string routePrefix,
        ProxyStaticResponseOptions response)
    {
        if (response.StatusCode is < 200 or > 599)
        {
            failures.Add($"{routePrefix}:StaticResponse:StatusCode must be between 200 and 599.");
        }

        if (string.IsNullOrWhiteSpace(response.ContentType)
            || response.ContentType.Any(static character => character is '\r' or '\n'))
        {
            failures.Add($"{routePrefix}:StaticResponse:ContentType must be a non-empty single-line value.");
        }

        if (Encoding.UTF8.GetByteCount(response.Body) > MaxGeneratedBodyBytes)
        {
            failures.Add($"{routePrefix}:StaticResponse:Body must not exceed {MaxGeneratedBodyBytes} UTF-8 bytes.");
        }
    }

    private static void ValidateMaintenance(
        List<string> failures,
        string routePrefix,
        ProxyMaintenanceOptions maintenance)
    {
        if (maintenance.RetryAfterSeconds is < 0 or > 86400)
        {
            failures.Add($"{routePrefix}:Maintenance:RetryAfterSeconds must be between 0 and 86400.");
        }

        if (string.IsNullOrWhiteSpace(maintenance.ContentType)
            || maintenance.ContentType.Any(static character => character is '\r' or '\n'))
        {
            failures.Add($"{routePrefix}:Maintenance:ContentType must be a non-empty single-line value.");
        }

        if (Encoding.UTF8.GetByteCount(maintenance.Body) > MaxGeneratedBodyBytes)
        {
            failures.Add($"{routePrefix}:Maintenance:Body must not exceed {MaxGeneratedBodyBytes} UTF-8 bytes.");
        }
    }

}
