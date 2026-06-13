namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed partial class RouteMatchDiagnosticsService
{
    private static RouteMatchDryRunUpstream? SelectDiagnosticUpstream(IProxyRouteDiagnosticsRoute route)
    {
        var upstream = route.Upstreams.FirstOrDefault(static candidate => candidate.Weight > 0);
        return upstream is null
            ? null
            : new RouteMatchDryRunUpstream(
                upstream.Name,
                upstream.Scheme,
                upstream.Protocol,
                upstream.Endpoint,
                upstream.Weight,
                "first_configured_candidate_no_state_mutation");
    }

    private static string EffectiveAction(IProxyRouteDiagnosticsRoute route, ProxyRouteDiagnosticsActionDecision actionDecision)
    {
        if (actionDecision.ShouldProxy)
        {
            return "proxy";
        }

        if (route.MaintenanceEnabled)
        {
            return "maintenance";
        }

        if (string.Equals(route.Action, "Proxy", StringComparison.OrdinalIgnoreCase))
        {
            return "policyRedirect";
        }

        return RouteActionText(route.Action);
    }

    private static string RouteActionText(string action)
    {
        if (string.Equals(action, "Redirect", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "redirect", StringComparison.OrdinalIgnoreCase))
        {
            return "redirect";
        }

        if (string.Equals(action, "StaticResponse", StringComparison.OrdinalIgnoreCase)
            || string.Equals(action, "staticResponse", StringComparison.OrdinalIgnoreCase))
        {
            return "staticResponse";
        }

        return "proxy";
    }

    private static RouteMatchDryRunListener ToListener(IProxyRouteDiagnosticsListener listener)
    {
        return new RouteMatchDryRunListener(
            listener.Name,
            string.Equals(listener.Transport, "https", StringComparison.OrdinalIgnoreCase) ? "https" : "http",
            listener.Address,
            listener.Port,
            listener.Protocols.ToString());
    }
}
