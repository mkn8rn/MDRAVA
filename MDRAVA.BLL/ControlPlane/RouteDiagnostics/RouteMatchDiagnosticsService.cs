using MDRAVA.BLL.ControlPlane.RuntimeGuards;

namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed partial class RouteMatchDiagnosticsService : IProxyRouteDiagnosticsOperations
{
    private readonly IProxyRouteDiagnosticsConfigurationSource _configurationSource;
    private readonly IProxyRouteDiagnosticsMatcher _routeMatcher;
    private readonly IProxyRouteDiagnosticsActionPolicy _routeActionPolicy;
    private readonly IProxyRouteDiagnosticsPathRewritePolicy _pathRewritePolicy;
    private readonly IProxyRouteDiagnosticsMetricsSink _metricsSink;
    private readonly IProxyClientAddressSyntaxPolicy _clientAddressSyntaxPolicy;
    private readonly TimeProvider _timeProvider;

    public RouteMatchDiagnosticsService(
        IProxyRouteDiagnosticsConfigurationSource configurationSource,
        IProxyRouteDiagnosticsMatcher routeMatcher,
        IProxyRouteDiagnosticsActionPolicy routeActionPolicy,
        IProxyRouteDiagnosticsPathRewritePolicy pathRewritePolicy,
        IProxyRouteDiagnosticsMetricsSink metricsSink,
        IProxyClientAddressSyntaxPolicy clientAddressSyntaxPolicy,
        TimeProvider timeProvider)
    {
        _configurationSource = configurationSource;
        _routeMatcher = routeMatcher;
        _routeActionPolicy = routeActionPolicy;
        _pathRewritePolicy = pathRewritePolicy;
        _metricsSink = metricsSink;
        _clientAddressSyntaxPolicy = clientAddressSyntaxPolicy;
        _timeProvider = timeProvider;
    }

    public RouteMatchDryRunResult Explain(RouteMatchDryRunRequest? request)
    {
        var evaluatedAtUtc = _timeProvider.GetUtcNow();
        var configuration = _configurationSource.Read();
        if (configuration is not ProxyRouteDiagnosticsConfigurationReadResult.AvailableResult available)
        {
            return Complete(Failure(evaluatedAtUtc, "no_active_config", "No active proxy configuration is loaded."));
        }

        var snapshot = available.Snapshot;
        var requestDecision = ProxyRouteDiagnosticsRequestReader.Read(
            request,
            evaluatedAtUtc,
            _clientAddressSyntaxPolicy);
        if (requestDecision is ProxyRouteDiagnosticsRequestDecision.RejectedDecision rejectedRequest)
        {
            return Complete(rejectedRequest.Failure);
        }

        var requestInput = ((ProxyRouteDiagnosticsRequestDecision.AcceptedDecision)requestDecision).Input;
        var listener = ProxyRouteDiagnosticsListenerSelector.Select(
            snapshot.Listeners,
            requestInput.ListenerName,
            requestInput.Scheme,
            requestInput.Port,
            requestInput.Protocol);
        if (listener is null)
        {
            return Complete(RouteMatchDryRunResult.NoMatchingListener(
                evaluatedAtUtc,
                requestInput.Target));
        }

        var route = _routeMatcher.Match(snapshot.Routes, requestInput.RequestHead);
        if (route is null)
        {
            return Complete(RouteMatchDryRunResult.NoMatchingRoute(
                evaluatedAtUtc,
                ToListener(listener),
                requestInput.Target));
        }

        var actionDecision = _routeActionPolicy.Evaluate(
            route,
            requestInput.RequestHead,
            listener,
            requestInput.IsUpgradeRequest);
        var generatedStatusCode = actionDecision.GeneratedStatusCode;
        var effectiveAction = EffectiveAction(route, actionDecision);
        var rewrittenTarget = _pathRewritePolicy.Apply(route, requestInput.Target, requestInput.Path);
        var wouldProxy = actionDecision.ShouldProxy;

        string? noMatchReason = null;
        if (wouldProxy
            && requestInput.RequestHead.Framing.Kind == ProxyRouteDiagnosticsBodyKind.ContentLength
            && requestInput.RequestHead.Framing.ContentLength.GetValueOrDefault() > route.MaxRequestBodyBytes)
        {
            wouldProxy = false;
            noMatchReason = "request_body_too_large";
            requestInput.AddFinding(new RouteMatchDryRunFinding("warning", "request_body_too_large", "The request body would exceed the matched route body limit."));
        }

        var upstream = wouldProxy ? SelectDiagnosticUpstream(route) : null;
        if (wouldProxy && upstream is null)
        {
            noMatchReason = "no_configured_upstream";
            requestInput.AddFinding(new RouteMatchDryRunFinding("warning", "no_configured_upstream", "The matched proxy route has no configured upstream candidate."));
        }

        return Complete(RouteMatchDryRunResult.MatchedRoute(
            evaluatedAtUtc,
            noMatchReason,
            ToListener(listener),
            new RouteMatchDryRunRoute(route.SiteName, route.Name, route.Host, route.PathPrefix),
            RouteActionText(route.Action),
            effectiveAction,
            wouldProxy && upstream is not null,
            generatedStatusCode,
            requestInput.Target,
            rewrittenTarget,
            upstream,
            ProxyRouteDiagnosticsPolicyExplainer.ExplainCache(route, requestInput.RequestHead, actionDecision.ShouldProxy),
            ProxyRouteDiagnosticsPolicyExplainer.ExplainRetry(route, requestInput.RequestHead, actionDecision.ShouldProxy),
            ProxyRouteDiagnosticsPolicyExplainer.ExplainCircuitBreaker(route, actionDecision.ShouldProxy),
            requestInput.Findings));
    }

    private RouteMatchDryRunResult Complete(RouteMatchDryRunResult result)
    {
        _metricsSink.RouteMatchDryRun(MetricReason(result));
        return result;
    }

    private static RouteMatchDryRunResult Failure(DateTimeOffset evaluatedAtUtc, string reason, string message)
    {
        return RouteMatchDryRunResult.Failed(evaluatedAtUtc, reason, message);
    }
}
