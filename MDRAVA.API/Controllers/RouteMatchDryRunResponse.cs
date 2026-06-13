using BusinessRouteMatchDryRunListener = MDRAVA.BLL.ControlPlane.RouteDiagnostics.RouteMatchDryRunListener;
using BusinessRouteMatchDryRunResult = MDRAVA.BLL.ControlPlane.RouteDiagnostics.RouteMatchDryRunResult;
using BusinessRouteMatchDryRunRoute = MDRAVA.BLL.ControlPlane.RouteDiagnostics.RouteMatchDryRunRoute;
using BusinessRouteMatchDryRunUpstream = MDRAVA.BLL.ControlPlane.RouteDiagnostics.RouteMatchDryRunUpstream;

namespace MDRAVA.API.Controllers;

public sealed record RouteMatchDryRunResponse(
    bool Succeeded,
    DateTimeOffset EvaluatedAtUtc,
    string? FailureReason,
    string? NoMatchReason,
    RouteMatchDryRunListenerResponse? Listener,
    RouteMatchDryRunRouteResponse? Route,
    string? ConfiguredAction,
    string? EffectiveAction,
    bool WouldProxy,
    int? GeneratedStatusCode,
    string? OriginalTarget,
    string? RewrittenTarget,
    RouteMatchDryRunUpstreamResponse? Upstream,
    RouteMatchDryRunPolicyResponse Cache,
    RouteMatchDryRunPolicyResponse Retry,
    RouteMatchDryRunPolicyResponse CircuitBreaker,
    IReadOnlyList<RouteMatchDryRunFindingResponse> Findings)
{
    public static RouteMatchDryRunResponse FromResult(BusinessRouteMatchDryRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result switch
        {
            BusinessRouteMatchDryRunResult.FailedResult failed => FromResult(
                failed,
                succeeded: false,
                failureReason: failed.FailureReason,
                noMatchReason: null,
                listener: null,
                route: null,
                configuredAction: null,
                effectiveAction: null,
                wouldProxy: false,
                generatedStatusCode: null,
                originalTarget: null,
                rewrittenTarget: null,
                upstream: null),
            BusinessRouteMatchDryRunResult.NoMatchingListenerResult noListener => FromResult(
                noListener,
                succeeded: true,
                failureReason: null,
                noMatchReason: noListener.NoMatchReason,
                listener: null,
                route: null,
                configuredAction: null,
                effectiveAction: null,
                wouldProxy: false,
                generatedStatusCode: null,
                originalTarget: noListener.OriginalTarget,
                rewrittenTarget: null,
                upstream: null),
            BusinessRouteMatchDryRunResult.NoMatchingRouteResult noRoute => FromResult(
                noRoute,
                succeeded: true,
                failureReason: null,
                noMatchReason: noRoute.NoMatchReason,
                listener: noRoute.Listener,
                route: null,
                configuredAction: null,
                effectiveAction: null,
                wouldProxy: false,
                generatedStatusCode: null,
                originalTarget: noRoute.OriginalTarget,
                rewrittenTarget: null,
                upstream: null),
            BusinessRouteMatchDryRunResult.MatchedRouteResult matched => FromResult(
                matched,
                succeeded: true,
                failureReason: null,
                noMatchReason: matched.NoMatchReason,
                listener: matched.Listener,
                route: matched.Route,
                configuredAction: matched.ConfiguredAction,
                effectiveAction: matched.EffectiveAction,
                wouldProxy: matched.WouldProxy,
                generatedStatusCode: matched.GeneratedStatusCode,
                originalTarget: matched.OriginalTarget,
                rewrittenTarget: matched.RewrittenTarget,
                upstream: matched.Upstream),
            _ => throw new InvalidOperationException($"Unknown route dry-run result '{result.GetType().Name}'.")
        };
    }

    private static RouteMatchDryRunResponse FromResult(
        BusinessRouteMatchDryRunResult result,
        bool succeeded,
        string? failureReason,
        string? noMatchReason,
        BusinessRouteMatchDryRunListener? listener,
        BusinessRouteMatchDryRunRoute? route,
        string? configuredAction,
        string? effectiveAction,
        bool wouldProxy,
        int? generatedStatusCode,
        string? originalTarget,
        string? rewrittenTarget,
        BusinessRouteMatchDryRunUpstream? upstream)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new RouteMatchDryRunResponse(
            Succeeded: succeeded,
            EvaluatedAtUtc: result.EvaluatedAtUtc,
            FailureReason: failureReason,
            NoMatchReason: noMatchReason,
            Listener: listener is null ? null : RouteMatchDryRunListenerResponse.FromListener(listener),
            Route: route is null ? null : RouteMatchDryRunRouteResponse.FromRoute(route),
            ConfiguredAction: configuredAction,
            EffectiveAction: effectiveAction,
            WouldProxy: wouldProxy,
            GeneratedStatusCode: generatedStatusCode,
            OriginalTarget: originalTarget,
            RewrittenTarget: rewrittenTarget,
            Upstream: upstream is null ? null : RouteMatchDryRunUpstreamResponse.FromUpstream(upstream),
            Cache: RouteMatchDryRunPolicyResponse.FromPolicy(result.Cache),
            Retry: RouteMatchDryRunPolicyResponse.FromPolicy(result.Retry),
            CircuitBreaker: RouteMatchDryRunPolicyResponse.FromPolicy(result.CircuitBreaker),
            Findings: RouteMatchDryRunFindingResponse.FromFindings(result.Findings));
    }
}
