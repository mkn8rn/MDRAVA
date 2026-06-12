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
        AddPerRouteFindings(snapshot, sourceName, findings);
        return findings;
    }

    private static void AddPerRouteFindings(
        ProxyConfigLintConfigurationSnapshot snapshot,
        string? sourceName,
        List<ConfigLintFinding> findings)
    {
        foreach (var route in snapshot.Routes)
        {
            var routePath = ConfigLintRouteIdentityPolicy.RoutePath(route);
            if (route.CanonicalHostEnabled
                && ConfigLintRouteIdentityPolicy.HostEquals(route.Host, route.CanonicalHostTargetHost))
            {
                findings.Add(ConfigLintFindingFactory.Warning("canonical_host_loop", $"Route '{route.Name}' canonical host target equals its configured host.", sourceName, routePath, "Remove the canonical host policy or set a different target host."));
            }

            findings.AddRange(ConfigLintRouteCacheAnalyzer.Analyze(route, routePath, sourceName));
            findings.AddRange(ConfigLintRouteResilienceAnalyzer.Analyze(route, routePath, sourceName));
            findings.AddRange(ConfigLintUpstreamAnalyzer.Analyze(snapshot, route, routePath, sourceName));

            if (string.Equals(route.Action, "StaticResponse", StringComparison.Ordinal)
                && Encoding.UTF8.GetByteCount(route.StaticResponseBody) >= MaxGeneratedBodyBytes * 4 / 5)
            {
                findings.Add(ConfigLintFindingFactory.Warning("static_response_body_near_limit", $"Static response route '{route.Name}' has a body near the generated-response size limit.", sourceName, routePath, "Move larger content behind an upstream application or keep the static body small."));
            }
        }
    }

}
