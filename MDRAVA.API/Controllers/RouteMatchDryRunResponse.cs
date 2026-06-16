using BusinessRouteMatchDryRunListener = MDRAVA.BLL.ControlPlane.RouteDiagnostics.RouteMatchDryRunListener;
using BusinessRouteMatchDryRunResult = MDRAVA.BLL.ControlPlane.RouteDiagnostics.RouteMatchDryRunResult;
using BusinessRouteMatchDryRunRoute = MDRAVA.BLL.ControlPlane.RouteDiagnostics.RouteMatchDryRunRoute;
using BusinessRouteMatchDryRunUpstream = MDRAVA.BLL.ControlPlane.RouteDiagnostics.RouteMatchDryRunUpstream;

namespace MDRAVA.API.Controllers;

public sealed record RouteMatchDryRunResponse
{
    public RouteMatchDryRunResponse(
        bool succeeded,
        DateTimeOffset evaluatedAtUtc,
        string? failureReason,
        string? noMatchReason,
        RouteMatchDryRunListenerResponse? listener,
        RouteMatchDryRunRouteResponse? route,
        string? configuredAction,
        string? effectiveAction,
        bool wouldProxy,
        int? generatedStatusCode,
        string? originalTarget,
        string? rewrittenTarget,
        RouteMatchDryRunUpstreamResponse? upstream,
        RouteMatchDryRunPolicyResponse cache,
        RouteMatchDryRunPolicyResponse retry,
        RouteMatchDryRunPolicyResponse circuitBreaker,
        IReadOnlyList<RouteMatchDryRunFindingResponse> findings)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(retry);
        ArgumentNullException.ThrowIfNull(circuitBreaker);

        Succeeded = succeeded;
        EvaluatedAtUtc = evaluatedAtUtc;
        FailureReason = failureReason;
        NoMatchReason = noMatchReason;
        Listener = listener;
        Route = route;
        ConfiguredAction = configuredAction;
        EffectiveAction = effectiveAction;
        WouldProxy = wouldProxy;
        GeneratedStatusCode = generatedStatusCode;
        OriginalTarget = originalTarget;
        RewrittenTarget = rewrittenTarget;
        Upstream = upstream;
        Cache = cache;
        Retry = retry;
        CircuitBreaker = circuitBreaker;
        Findings = ApiResponseList.Copy(findings);
    }

    public bool Succeeded { get; }

    public DateTimeOffset EvaluatedAtUtc { get; }

    public string? FailureReason { get; }

    public string? NoMatchReason { get; }

    public RouteMatchDryRunListenerResponse? Listener { get; }

    public RouteMatchDryRunRouteResponse? Route { get; }

    public string? ConfiguredAction { get; }

    public string? EffectiveAction { get; }

    public bool WouldProxy { get; }

    public int? GeneratedStatusCode { get; }

    public string? OriginalTarget { get; }

    public string? RewrittenTarget { get; }

    public RouteMatchDryRunUpstreamResponse? Upstream { get; }

    public RouteMatchDryRunPolicyResponse Cache { get; }

    public RouteMatchDryRunPolicyResponse Retry { get; }

    public RouteMatchDryRunPolicyResponse CircuitBreaker { get; }

    public IReadOnlyList<RouteMatchDryRunFindingResponse> Findings { get; }

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
            succeeded: succeeded,
            evaluatedAtUtc: result.EvaluatedAtUtc,
            failureReason: failureReason,
            noMatchReason: noMatchReason,
            listener: listener is null ? null : RouteMatchDryRunListenerResponse.FromListener(listener),
            route: route is null ? null : RouteMatchDryRunRouteResponse.FromRoute(route),
            configuredAction: configuredAction,
            effectiveAction: effectiveAction,
            wouldProxy: wouldProxy,
            generatedStatusCode: generatedStatusCode,
            originalTarget: originalTarget,
            rewrittenTarget: rewrittenTarget,
            upstream: upstream is null ? null : RouteMatchDryRunUpstreamResponse.FromUpstream(upstream),
            cache: RouteMatchDryRunPolicyResponse.FromPolicy(result.Cache),
            retry: RouteMatchDryRunPolicyResponse.FromPolicy(result.Retry),
            circuitBreaker: RouteMatchDryRunPolicyResponse.FromPolicy(result.CircuitBreaker),
            findings: RouteMatchDryRunFindingResponse.FromFindings(result.Findings));
    }
}
