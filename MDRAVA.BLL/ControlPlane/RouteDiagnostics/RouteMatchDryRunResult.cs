namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed record RouteMatchDryRunResult
{
    private RouteMatchDryRunResult(
        bool succeeded,
        DateTimeOffset evaluatedAtUtc,
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
        RouteMatchDryRunUpstream? upstream,
        RouteMatchDryRunPolicy cache,
        RouteMatchDryRunPolicy retry,
        RouteMatchDryRunPolicy circuitBreaker,
        IReadOnlyList<RouteMatchDryRunFinding> findings)
    {
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
        Findings = findings;
    }

    public bool Succeeded { get; }

    public DateTimeOffset EvaluatedAtUtc { get; }

    public string? FailureReason { get; }

    public string? NoMatchReason { get; }

    public RouteMatchDryRunListener? Listener { get; }

    public RouteMatchDryRunRoute? Route { get; }

    public string? ConfiguredAction { get; }

    public string? EffectiveAction { get; }

    public bool WouldProxy { get; }

    public int? GeneratedStatusCode { get; }

    public string? OriginalTarget { get; }

    public string? RewrittenTarget { get; }

    public RouteMatchDryRunUpstream? Upstream { get; }

    public RouteMatchDryRunPolicy Cache { get; }

    public RouteMatchDryRunPolicy Retry { get; }

    public RouteMatchDryRunPolicy CircuitBreaker { get; }

    public IReadOnlyList<RouteMatchDryRunFinding> Findings { get; }

    public static RouteMatchDryRunResult Failed(
        DateTimeOffset evaluatedAtUtc,
        string reason,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new RouteMatchDryRunResult(
            succeeded: false,
            evaluatedAtUtc,
            failureReason: reason,
            noMatchReason: null,
            listener: null,
            route: null,
            configuredAction: null,
            effectiveAction: null,
            wouldProxy: false,
            generatedStatusCode: null,
            originalTarget: null,
            rewrittenTarget: null,
            upstream: null,
            cache: ProxyRouteDiagnosticsPolicyExplainer.Disabled(reason),
            retry: ProxyRouteDiagnosticsPolicyExplainer.Disabled(reason),
            circuitBreaker: ProxyRouteDiagnosticsPolicyExplainer.Disabled(reason),
            findings: [new RouteMatchDryRunFinding("error", reason, message)]);
    }

    public static RouteMatchDryRunResult NoMatchingListener(
        DateTimeOffset evaluatedAtUtc,
        string originalTarget)
    {
        return new RouteMatchDryRunResult(
            succeeded: true,
            evaluatedAtUtc,
            failureReason: null,
            noMatchReason: "no_matching_listener",
            listener: null,
            route: null,
            configuredAction: null,
            effectiveAction: null,
            wouldProxy: false,
            generatedStatusCode: null,
            originalTarget,
            rewrittenTarget: null,
            upstream: null,
            cache: ProxyRouteDiagnosticsPolicyExplainer.Disabled("no_route"),
            retry: ProxyRouteDiagnosticsPolicyExplainer.Disabled("no_route"),
            circuitBreaker: ProxyRouteDiagnosticsPolicyExplainer.Disabled("no_route"),
            findings: [new RouteMatchDryRunFinding("warning", "no_matching_listener", "No enabled listener matches the supplied scheme, port, or listener identity.")]);
    }

    public static RouteMatchDryRunResult NoMatchingRoute(
        DateTimeOffset evaluatedAtUtc,
        RouteMatchDryRunListener listener,
        string originalTarget)
    {
        ArgumentNullException.ThrowIfNull(listener);

        return new RouteMatchDryRunResult(
            succeeded: true,
            evaluatedAtUtc,
            failureReason: null,
            noMatchReason: "no_matching_route",
            listener,
            route: null,
            configuredAction: null,
            effectiveAction: null,
            wouldProxy: false,
            generatedStatusCode: null,
            originalTarget,
            rewrittenTarget: null,
            upstream: null,
            cache: ProxyRouteDiagnosticsPolicyExplainer.Disabled("no_route"),
            retry: ProxyRouteDiagnosticsPolicyExplainer.Disabled("no_route"),
            circuitBreaker: ProxyRouteDiagnosticsPolicyExplainer.Disabled("no_route"),
            findings: [new RouteMatchDryRunFinding("info", "no_matching_route", "No configured route matched the supplied host and path.")]);
    }

    public static RouteMatchDryRunResult MatchedRoute(
        DateTimeOffset evaluatedAtUtc,
        string? noMatchReason,
        RouteMatchDryRunListener listener,
        RouteMatchDryRunRoute route,
        string configuredAction,
        string effectiveAction,
        bool wouldProxy,
        int? generatedStatusCode,
        string originalTarget,
        string rewrittenTarget,
        RouteMatchDryRunUpstream? upstream,
        RouteMatchDryRunPolicy cache,
        RouteMatchDryRunPolicy retry,
        RouteMatchDryRunPolicy circuitBreaker,
        IReadOnlyList<RouteMatchDryRunFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(listener);
        ArgumentNullException.ThrowIfNull(route);
        ArgumentException.ThrowIfNullOrWhiteSpace(configuredAction);
        ArgumentException.ThrowIfNullOrWhiteSpace(effectiveAction);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(retry);
        ArgumentNullException.ThrowIfNull(circuitBreaker);
        ArgumentNullException.ThrowIfNull(findings);
        if (wouldProxy && upstream is null)
        {
            throw new ArgumentException("A proxying route dry-run requires an upstream.", nameof(upstream));
        }

        return new RouteMatchDryRunResult(
            succeeded: true,
            evaluatedAtUtc,
            failureReason: null,
            noMatchReason,
            listener,
            route,
            configuredAction,
            effectiveAction,
            wouldProxy,
            generatedStatusCode,
            originalTarget,
            rewrittenTarget,
            upstream,
            cache,
            retry,
            circuitBreaker,
            findings);
    }
}
