namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public abstract partial record RouteMatchDryRunResult
{
    private RouteMatchDryRunResult(
        DateTimeOffset evaluatedAtUtc,
        RouteMatchDryRunPolicy cache,
        RouteMatchDryRunPolicy retry,
        RouteMatchDryRunPolicy circuitBreaker,
        IReadOnlyList<RouteMatchDryRunFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(retry);
        ArgumentNullException.ThrowIfNull(circuitBreaker);
        ArgumentNullException.ThrowIfNull(findings);

        EvaluatedAtUtc = evaluatedAtUtc;
        Cache = cache;
        Retry = retry;
        CircuitBreaker = circuitBreaker;
        Findings = new List<RouteMatchDryRunFinding>(findings).AsReadOnly();
    }

    public DateTimeOffset EvaluatedAtUtc { get; }

    public RouteMatchDryRunPolicy Cache { get; }

    public RouteMatchDryRunPolicy Retry { get; }

    public RouteMatchDryRunPolicy CircuitBreaker { get; }

    public IReadOnlyList<RouteMatchDryRunFinding> Findings { get; }

    public static FailedResult Failed(
        DateTimeOffset evaluatedAtUtc,
        string reason,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new FailedResult(
            evaluatedAtUtc,
            reason,
            ProxyRouteDiagnosticsPolicyExplainer.Disabled(reason),
            ProxyRouteDiagnosticsPolicyExplainer.Disabled(reason),
            ProxyRouteDiagnosticsPolicyExplainer.Disabled(reason),
            [new RouteMatchDryRunFinding("error", reason, message)]);
    }

    public static NoMatchingListenerResult NoMatchingListener(
        DateTimeOffset evaluatedAtUtc,
        string originalTarget)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(originalTarget);

        return new NoMatchingListenerResult(
            evaluatedAtUtc,
            "no_matching_listener",
            originalTarget,
            ProxyRouteDiagnosticsPolicyExplainer.Disabled("no_route"),
            ProxyRouteDiagnosticsPolicyExplainer.Disabled("no_route"),
            ProxyRouteDiagnosticsPolicyExplainer.Disabled("no_route"),
            [new RouteMatchDryRunFinding("warning", "no_matching_listener", "No enabled listener matches the supplied scheme, port, or listener identity.")]);
    }

    public static NoMatchingRouteResult NoMatchingRoute(
        DateTimeOffset evaluatedAtUtc,
        RouteMatchDryRunListener listener,
        string originalTarget)
    {
        ArgumentNullException.ThrowIfNull(listener);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalTarget);

        return new NoMatchingRouteResult(
            evaluatedAtUtc,
            "no_matching_route",
            listener,
            originalTarget,
            ProxyRouteDiagnosticsPolicyExplainer.Disabled("no_route"),
            ProxyRouteDiagnosticsPolicyExplainer.Disabled("no_route"),
            ProxyRouteDiagnosticsPolicyExplainer.Disabled("no_route"),
            [new RouteMatchDryRunFinding("info", "no_matching_route", "No configured route matched the supplied host and path.")]);
    }

    public static MatchedRouteResult MatchedRoute(
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
        ArgumentException.ThrowIfNullOrWhiteSpace(originalTarget);
        ArgumentException.ThrowIfNullOrWhiteSpace(rewrittenTarget);
        if (wouldProxy && upstream is null)
        {
            throw new ArgumentException("A proxying route dry-run requires an upstream.", nameof(upstream));
        }

        return new MatchedRouteResult(
            evaluatedAtUtc,
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
