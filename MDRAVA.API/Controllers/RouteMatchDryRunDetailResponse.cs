using BusinessRouteMatchDryRunFinding = MDRAVA.BLL.ControlPlane.RouteDiagnostics.RouteMatchDryRunFinding;
using BusinessRouteMatchDryRunListener = MDRAVA.BLL.ControlPlane.RouteDiagnostics.RouteMatchDryRunListener;
using BusinessRouteMatchDryRunPolicy = MDRAVA.BLL.ControlPlane.RouteDiagnostics.RouteMatchDryRunPolicy;
using BusinessRouteMatchDryRunRoute = MDRAVA.BLL.ControlPlane.RouteDiagnostics.RouteMatchDryRunRoute;
using BusinessRouteMatchDryRunUpstream = MDRAVA.BLL.ControlPlane.RouteDiagnostics.RouteMatchDryRunUpstream;

namespace MDRAVA.API.Controllers;

public sealed record RouteMatchDryRunListenerResponse(
    string Name,
    string Transport,
    string Address,
    int Port,
    string Protocols)
{
    public static RouteMatchDryRunListenerResponse FromListener(BusinessRouteMatchDryRunListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        return new RouteMatchDryRunListenerResponse(
            listener.Name,
            listener.Transport,
            listener.Address,
            listener.Port,
            listener.Protocols);
    }
}

public sealed record RouteMatchDryRunRouteResponse(
    string SiteName,
    string Name,
    string Host,
    string PathPrefix)
{
    public static RouteMatchDryRunRouteResponse FromRoute(BusinessRouteMatchDryRunRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return new RouteMatchDryRunRouteResponse(
            route.SiteName,
            route.Name,
            route.Host,
            route.PathPrefix);
    }
}

public sealed record RouteMatchDryRunUpstreamResponse(
    string Name,
    string Scheme,
    string Protocol,
    string Endpoint,
    int Weight,
    string SelectionReason)
{
    public static RouteMatchDryRunUpstreamResponse FromUpstream(BusinessRouteMatchDryRunUpstream upstream)
    {
        ArgumentNullException.ThrowIfNull(upstream);

        return new RouteMatchDryRunUpstreamResponse(
            upstream.Name,
            upstream.Scheme,
            upstream.Protocol,
            upstream.Endpoint,
            upstream.Weight,
            upstream.SelectionReason);
    }
}

public sealed record RouteMatchDryRunPolicyResponse(
    bool Enabled,
    bool WouldApply,
    string Reason)
{
    public static RouteMatchDryRunPolicyResponse FromPolicy(BusinessRouteMatchDryRunPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new RouteMatchDryRunPolicyResponse(
            policy.Enabled,
            policy.WouldApply,
            policy.Reason);
    }
}

public sealed record RouteMatchDryRunFindingResponse(
    string Severity,
    string Code,
    string Message)
{
    public static IReadOnlyList<RouteMatchDryRunFindingResponse> FromFindings(
        IReadOnlyList<BusinessRouteMatchDryRunFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);

        return findings.Select(FromFinding).ToArray();
    }

    private static RouteMatchDryRunFindingResponse FromFinding(BusinessRouteMatchDryRunFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);

        return new RouteMatchDryRunFindingResponse(
            finding.Severity,
            finding.Code,
            finding.Message);
    }
}
