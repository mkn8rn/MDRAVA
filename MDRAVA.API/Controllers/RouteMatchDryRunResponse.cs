using MDRAVA.BLL.ControlPlane.RouteDiagnostics;

namespace MDRAVA.API.Controllers;

public sealed record RouteMatchDryRunResponse(
    bool Succeeded,
    DateTimeOffset EvaluatedAtUtc,
    string? FailureReason,
    string? NoMatchReason,
    RouteMatchDryRunListener? Listener,
    RouteMatchDryRunRoute? Route,
    string? ConfiguredAction,
    string? EffectiveAction,
    bool WouldProxy,
    int? GeneratedStatusCode,
    string? OriginalTarget,
    string? RewrittenTarget,
    RouteMatchDryRunUpstream? Upstream,
    RouteMatchDryRunPolicy Cache,
    RouteMatchDryRunPolicy Retry,
    RouteMatchDryRunPolicy CircuitBreaker,
    IReadOnlyList<RouteMatchDryRunFinding> Findings)
{
    public static RouteMatchDryRunResponse FromResult(RouteMatchDryRunResult result)
    {
        return result switch
        {
            RouteMatchDryRunResult.FailedResult failed => FromResult(
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
            RouteMatchDryRunResult.NoMatchingListenerResult noListener => FromResult(
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
            RouteMatchDryRunResult.NoMatchingRouteResult noRoute => FromResult(
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
            RouteMatchDryRunResult.MatchedRouteResult matched => FromResult(
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
        RouteMatchDryRunResult result,
        bool succeeded,
        string? failureReason,
        string? noMatchReason,
        RouteMatchDryRunListener? listener,
        RouteMatchDryRunRoute? route,
        string? configuredAction,
        string? effectiveAction,
        bool wouldProxy,
        int? generatedStatusCode,
        string? originalTarget,
        string? rewrittenTarget,
        RouteMatchDryRunUpstream? upstream)
    {
        return new RouteMatchDryRunResponse(
            Succeeded: succeeded,
            EvaluatedAtUtc: result.EvaluatedAtUtc,
            FailureReason: failureReason,
            NoMatchReason: noMatchReason,
            Listener: listener,
            Route: route,
            ConfiguredAction: configuredAction,
            EffectiveAction: effectiveAction,
            WouldProxy: wouldProxy,
            GeneratedStatusCode: generatedStatusCode,
            OriginalTarget: originalTarget,
            RewrittenTarget: rewrittenTarget,
            Upstream: upstream,
            Cache: result.Cache,
            Retry: result.Retry,
            CircuitBreaker: result.CircuitBreaker,
            Findings: result.Findings);
    }
}
