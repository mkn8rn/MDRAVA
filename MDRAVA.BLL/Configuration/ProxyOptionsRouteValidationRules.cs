using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.Configuration;

public static partial class ProxyOptionsValidationRules
{
    private static void ValidateRoutes(
        List<string> failures,
        ProxyOptions options,
        IProxyEndpointAddressPolicy endpointAddressPolicy,
        IProxyUrlSyntaxPolicy urlSyntaxPolicy)
    {
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

            ValidateUpstreams(failures, routePrefix, route, endpointAddressPolicy);
        }
    }

    private static void ValidateUpstreams(
        List<string> failures,
        string routePrefix,
        ProxyRouteOptions route,
        IProxyEndpointAddressPolicy endpointAddressPolicy)
    {
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
}
