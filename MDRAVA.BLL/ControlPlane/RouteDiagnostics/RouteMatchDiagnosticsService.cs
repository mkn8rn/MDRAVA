using MDRAVA.BLL.ControlPlane.RuntimeGuards;

namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed class RouteMatchDiagnosticsService : IProxyRouteDiagnosticsOperations
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
        if (!_configurationSource.TryRead(out var snapshot) || snapshot is null)
        {
            return Complete(Failure(evaluatedAtUtc, "no_active_config", "No active proxy configuration is loaded."));
        }

        if (!ProxyRouteDiagnosticsRequestReader.TryRead(
            request,
            evaluatedAtUtc,
            _clientAddressSyntaxPolicy,
            out var input,
            out var invalidResult))
        {
            return Complete(invalidResult!);
        }

        var requestInput = input!;
        var listener = ProxyRouteDiagnosticsListenerSelector.Select(
            snapshot,
            requestInput.ListenerName,
            requestInput.Scheme,
            requestInput.Port,
            requestInput.Protocol);
        if (listener is null)
        {
            return Complete(new RouteMatchDryRunResult(
                true,
                evaluatedAtUtc,
                null,
                "no_matching_listener",
                null,
                null,
                null,
                null,
                false,
                null,
                requestInput.Target,
                null,
                null,
                ProxyRouteDiagnosticsPolicyExplainer.Disabled("no_route"),
                ProxyRouteDiagnosticsPolicyExplainer.Disabled("no_route"),
                ProxyRouteDiagnosticsPolicyExplainer.Disabled("no_route"),
                [new RouteMatchDryRunFinding("warning", "no_matching_listener", "No enabled listener matches the supplied scheme, port, or listener identity.")]));
        }

        var route = _routeMatcher.Match(snapshot.Routes, requestInput.RequestHead);
        if (route is null)
        {
            return Complete(new RouteMatchDryRunResult(
                true,
                evaluatedAtUtc,
                null,
                "no_matching_route",
                ToListener(listener),
                null,
                null,
                null,
                false,
                null,
                requestInput.Target,
                null,
                null,
                ProxyRouteDiagnosticsPolicyExplainer.Disabled("no_route"),
                ProxyRouteDiagnosticsPolicyExplainer.Disabled("no_route"),
                ProxyRouteDiagnosticsPolicyExplainer.Disabled("no_route"),
                [new RouteMatchDryRunFinding("info", "no_matching_route", "No configured route matched the supplied host and path.")]));
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
            requestInput.Findings.Add(new RouteMatchDryRunFinding("warning", "request_body_too_large", "The request body would exceed the matched route body limit."));
        }

        var upstream = wouldProxy ? SelectDiagnosticUpstream(route) : null;
        if (wouldProxy && upstream is null)
        {
            noMatchReason = "no_configured_upstream";
            requestInput.Findings.Add(new RouteMatchDryRunFinding("warning", "no_configured_upstream", "The matched proxy route has no configured upstream candidate."));
        }

        return Complete(new RouteMatchDryRunResult(
            true,
            evaluatedAtUtc,
            null,
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
        _metricsSink.RouteMatchDryRun(result.FailureReason ?? result.NoMatchReason);
        return result;
    }

    private static RouteMatchDryRunResult Failure(DateTimeOffset evaluatedAtUtc, string reason, string message)
    {
        return new RouteMatchDryRunResult(
            false,
            evaluatedAtUtc,
            reason,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            ProxyRouteDiagnosticsPolicyExplainer.Disabled(reason),
            ProxyRouteDiagnosticsPolicyExplainer.Disabled(reason),
            ProxyRouteDiagnosticsPolicyExplainer.Disabled(reason),
            [new RouteMatchDryRunFinding("error", reason, message)]);
    }

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
