namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public static class ConfigLintRouteAnalyzer
{
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
            findings.AddRange(ConfigLintRouteGeneratedResponseAnalyzer.Analyze(route, routePath, sourceName));
        }
    }

}
