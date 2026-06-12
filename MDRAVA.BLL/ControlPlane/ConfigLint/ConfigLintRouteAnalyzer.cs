using MDRAVA.BLL.Http;
using System.Text;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public static class ConfigLintRouteAnalyzer
{
    private const int MaxGeneratedBodyBytes = 64 * 1024;

    public static IReadOnlyList<ConfigLintFinding> Analyze(
        ProxyConfigLintConfigurationSnapshot snapshot,
        string? sourceName)
    {
        List<ConfigLintFinding> findings = [];
        AddOrderingFindings(snapshot, sourceName, findings);
        AddIdentityFindings(snapshot, sourceName, findings);
        AddPerRouteFindings(snapshot, sourceName, findings);
        AddSiteFallbackFindings(snapshot, sourceName, findings);
        return findings;
    }

    private static void AddOrderingFindings(
        ProxyConfigLintConfigurationSnapshot snapshot,
        string? sourceName,
        List<ConfigLintFinding> findings)
    {
        for (var laterIndex = 0; laterIndex < snapshot.Routes.Count; laterIndex++)
        {
            var later = snapshot.Routes[laterIndex];
            var shadowReported = false;
            var broadCatchAllReported = false;
            for (var earlierIndex = 0; earlierIndex < laterIndex; earlierIndex++)
            {
                var earlier = snapshot.Routes[earlierIndex];
                if (!shadowReported && RouteShadows(earlier, later))
                {
                    findings.Add(Warning("route_shadowed", $"Route '{later.Name}' is shadowed by earlier route '{earlier.Name}'.", sourceName, RoutePath(later), "Move the more specific route before the broad route or narrow the earlier path prefix."));
                    shadowReported = true;
                }

                if (!broadCatchAllReported && IsBroadCatchAll(earlier) && HostOverlaps(earlier.Host, later.Host))
                {
                    findings.Add(Warning("broad_catch_all_before_specific", $"Catch-all route '{earlier.Name}' appears before more specific route '{later.Name}'.", sourceName, RoutePath(earlier), "Put catch-all routes last."));
                    broadCatchAllReported = true;
                }

                if (shadowReported && broadCatchAllReported)
                {
                    break;
                }
            }
        }
    }

    private static void AddIdentityFindings(
        ProxyConfigLintConfigurationSnapshot snapshot,
        string? sourceName,
        List<ConfigLintFinding> findings)
    {
        foreach (var group in snapshot.Routes.GroupBy(static route => $"{route.Host}|{route.PathPrefix}", StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() > 1)
            {
                findings.Add(Warning("overlapping_route_identity", $"Multiple routes use host/path identity '{group.Key}'.", sourceName, "routes", "Keep one route per host and path prefix or make ordering intentional."));
            }
        }
    }

    private static void AddPerRouteFindings(
        ProxyConfigLintConfigurationSnapshot snapshot,
        string? sourceName,
        List<ConfigLintFinding> findings)
    {
        foreach (var route in snapshot.Routes)
        {
            var routePath = RoutePath(route);
            if (route.CanonicalHostEnabled && HostEquals(route.Host, route.CanonicalHostTargetHost))
            {
                findings.Add(Warning("canonical_host_loop", $"Route '{route.Name}' canonical host target equals its configured host.", sourceName, routePath, "Remove the canonical host policy or set a different target host."));
            }

            if (route.CacheEnabled && LooksPrivate(route))
            {
                findings.Add(Warning("cache_private_path", $"Route '{route.Name}' enables cache on a path or header pattern that commonly serves private content.", sourceName, routePath, "Keep caching disabled for authenticated or user-specific resources."));
            }

            if (route.RetryEnabled && route.RetryMethods.Any(static method => !ProxyRequestMethodPolicy.IsSafeReadMethod(method)))
            {
                findings.Add(Error("retry_unsafe_method", $"Route '{route.Name}' allows retry for an unsafe method.", sourceName, routePath, "Restrict retry methods to GET and HEAD."));
            }

            if (route.Upstreams.Any(static upstream => upstream.CircuitBreakerEnabled)
                && (route.Upstreams.Count < 2 || !route.HealthCheckEnabled))
            {
                findings.Add(Warning("circuit_breaker_low_redundancy", $"Route '{route.Name}' configures a circuit breaker without multiple upstreams or active health checks.", sourceName, routePath, "Circuit breakers are most useful with redundant upstreams and health checks."));
            }

            findings.AddRange(ConfigLintUpstreamAnalyzer.Analyze(snapshot, route, routePath, sourceName));

            if (string.Equals(route.Action, "StaticResponse", StringComparison.Ordinal)
                && Encoding.UTF8.GetByteCount(route.StaticResponseBody) >= MaxGeneratedBodyBytes * 4 / 5)
            {
                findings.Add(Warning("static_response_body_near_limit", $"Static response route '{route.Name}' has a body near the generated-response size limit.", sourceName, routePath, "Move larger content behind an upstream application or keep the static body small."));
            }
        }
    }

    private static void AddSiteFallbackFindings(
        ProxyConfigLintConfigurationSnapshot snapshot,
        string? sourceName,
        List<ConfigLintFinding> findings)
    {
        foreach (var group in snapshot.Routes.GroupBy(static route => route.SiteName, StringComparer.OrdinalIgnoreCase))
        {
            if (!group.Any(static route => route.PathPrefix == "/"))
            {
                findings.Add(Info("site_without_fallback_route", $"Site '{group.Key}' has no '/' fallback route.", sourceName, $"sites[{group.Key}]", "Add an explicit fallback route if unmatched paths should have controlled behavior."));
            }
        }
    }

    private static bool RouteShadows(ProxyConfigLintRoute earlier, ProxyConfigLintRoute later)
    {
        return HostOverlaps(earlier.Host, later.Host)
            && later.PathPrefix.StartsWith(earlier.PathPrefix, StringComparison.Ordinal);
    }

    private static bool IsBroadCatchAll(ProxyConfigLintRoute route)
    {
        return string.Equals(route.Host, "*", StringComparison.Ordinal)
            && string.Equals(route.PathPrefix, "/", StringComparison.Ordinal);
    }

    private static bool HostOverlaps(string earlierHost, string laterHost)
    {
        return string.Equals(earlierHost, "*", StringComparison.Ordinal)
            || string.Equals(laterHost, "*", StringComparison.Ordinal)
            || HostEquals(earlierHost, laterHost);
    }

    private static bool HostEquals(string left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(StripPort(left), StripPort(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string StripPort(string host)
    {
        var colonIndex = host.LastIndexOf(':');
        return colonIndex <= 0 || host.Contains(']', StringComparison.Ordinal)
            ? host
            : host[..colonIndex];
    }

    private static bool LooksPrivate(ProxyConfigLintRoute route)
    {
        var path = route.PathPrefix.ToLowerInvariant();
        return path.Contains("admin", StringComparison.Ordinal)
            || path.Contains("auth", StringComparison.Ordinal)
            || path.Contains("account", StringComparison.Ordinal)
            || path.Contains("private", StringComparison.Ordinal)
            || path.Contains("profile", StringComparison.Ordinal)
            || path.Contains("user", StringComparison.Ordinal)
            || route.CacheVaryByHeaders.Any(static header => string.Equals(header, "Authorization", StringComparison.OrdinalIgnoreCase)
                || string.Equals(header, "Cookie", StringComparison.OrdinalIgnoreCase));
    }

    private static string RoutePath(ProxyConfigLintRoute route)
    {
        return $"sites[{route.SiteName}].routes[{route.Name}]";
    }

    private static ConfigLintFinding Info(
        string code,
        string message,
        string? source,
        string? path,
        string? suggestedFix)
    {
        return new ConfigLintFinding("info", code, message, source, path, suggestedFix);
    }

    private static ConfigLintFinding Warning(
        string code,
        string message,
        string? source,
        string? path,
        string? suggestedFix)
    {
        return new ConfigLintFinding("warning", code, message, source, path, suggestedFix);
    }

    private static ConfigLintFinding Error(
        string code,
        string message,
        string? source,
        string? path,
        string? suggestedFix)
    {
        return new ConfigLintFinding("error", code, message, source, path, suggestedFix);
    }
}
