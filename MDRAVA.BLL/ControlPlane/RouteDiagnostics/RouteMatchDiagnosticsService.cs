using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.RuntimeGuards;
using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed class RouteMatchDiagnosticsService : IProxyRouteDiagnosticsOperations
{
    private const int MaxInputLength = 4096;
    private static readonly HashSet<string> SensitiveHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Proxy-Authorization",
        "Set-Cookie",
        "X-MDRAVA-Admin-Key"
    };

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

        if (!TryValidateRequest(request, evaluatedAtUtc, _clientAddressSyntaxPolicy, out var invalidResult))
        {
            return Complete(invalidResult);
        }

        var scheme = NormalizeScheme(request!.Scheme);
        var protocol = NormalizeProtocol(request.Protocol);
        var method = NormalizeMethod(request.Method);
        var path = NormalizePath(request.Path);
        var query = NormalizeQuery(request.Query);
        var target = path + query;
        var findings = new List<RouteMatchDryRunFinding>();
        var listener = SelectListener(snapshot, request.ListenerName, scheme, request.Port, protocol);
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
                target,
                null,
                null,
                DisabledPolicy("no_route"),
                DisabledPolicy("no_route"),
                DisabledPolicy("no_route"),
                [new RouteMatchDryRunFinding("warning", "no_matching_listener", "No enabled listener matches the supplied scheme, port, or listener identity.")]));
        }

        var headers = BuildHeaders(request, findings);
        var framing = ResolveFraming(headers);
        var requestHead = new ProxyRouteDiagnosticsRequestHead(
            method,
            target,
            path,
            "HTTP/1.1",
            request.Host.Trim(),
            framing,
            headers);

        var route = _routeMatcher.Match(snapshot, requestHead);
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
                target,
                null,
                null,
                DisabledPolicy("no_route"),
                DisabledPolicy("no_route"),
                DisabledPolicy("no_route"),
                [new RouteMatchDryRunFinding("info", "no_matching_route", "No configured route matched the supplied host and path.")]));
        }

        var actionDecision = _routeActionPolicy.Evaluate(route, requestHead, listener, isUpgradeRequest: IsUpgrade(headers));
        var generatedStatusCode = actionDecision.GeneratedStatusCode;
        var effectiveAction = EffectiveAction(route, actionDecision);
        var rewrittenTarget = _pathRewritePolicy.Apply(route, target, path);
        var wouldProxy = actionDecision.ShouldProxy;

        string? noMatchReason = null;
        if (wouldProxy
            && framing.Kind == ProxyRouteDiagnosticsBodyKind.ContentLength
            && framing.ContentLength.GetValueOrDefault() > route.MaxRequestBodyBytes)
        {
            wouldProxy = false;
            noMatchReason = "request_body_too_large";
            findings.Add(new RouteMatchDryRunFinding("warning", "request_body_too_large", "The request body would exceed the matched route body limit."));
        }

        var upstream = wouldProxy ? SelectDiagnosticUpstream(route) : null;
        if (wouldProxy && upstream is null)
        {
            noMatchReason = "no_configured_upstream";
            findings.Add(new RouteMatchDryRunFinding("warning", "no_configured_upstream", "The matched proxy route has no configured upstream candidate."));
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
            target,
            rewrittenTarget,
            upstream,
            ExplainCache(route, requestHead, actionDecision.ShouldProxy),
            ExplainRetry(route, requestHead, actionDecision.ShouldProxy),
            ExplainCircuitBreaker(route, actionDecision.ShouldProxy),
            findings));
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
            DisabledPolicy(reason),
            DisabledPolicy(reason),
            DisabledPolicy(reason),
            [new RouteMatchDryRunFinding("error", reason, message)]);
    }

    private static bool TryValidateRequest(
        RouteMatchDryRunRequest? request,
        DateTimeOffset evaluatedAtUtc,
        IProxyClientAddressSyntaxPolicy clientAddressSyntaxPolicy,
        out RouteMatchDryRunResult result)
    {
        result = Failure(evaluatedAtUtc, "invalid_input", "The dry-run request is invalid.");
        if (request is null)
        {
            result = Failure(evaluatedAtUtc, "missing_request", "A dry-run request body is required.");
            return false;
        }

        var scheme = NormalizeScheme(request.Scheme);
        if (scheme is not "http" and not "https")
        {
            result = Failure(evaluatedAtUtc, "invalid_scheme", "Scheme must be 'http' or 'https'.");
            return false;
        }

        var protocol = NormalizeProtocol(request.Protocol);
        if (protocol is not null and not "http1" and not "http2" and not "http3")
        {
            result = Failure(evaluatedAtUtc, "invalid_protocol", "Protocol must be 'http1', 'http2', or 'http3' when supplied.");
            return false;
        }

        if (protocol == "http3" && scheme != "https")
        {
            result = Failure(evaluatedAtUtc, "invalid_protocol", "HTTP/3 dry-runs must use the https scheme.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Host) || request.Host.Length > 253 || ContainsControl(request.Host))
        {
            result = Failure(evaluatedAtUtc, "invalid_host", "Host is required and must not contain control characters.");
            return false;
        }

        if (request.Port is < 1 or > 65535)
        {
            result = Failure(evaluatedAtUtc, "invalid_port", "Port must be between 1 and 65535 when supplied.");
            return false;
        }

        var method = NormalizeMethod(request.Method);
        if (method.Length is 0 or > 32 || ContainsControl(method) || method.Any(static character => char.IsWhiteSpace(character)))
        {
            result = Failure(evaluatedAtUtc, "invalid_method", "Method must be a non-empty HTTP token.");
            return false;
        }

        var path = NormalizePath(request.Path);
        if (path.Length is 0 or > MaxInputLength || !path.StartsWith('/'))
        {
            result = Failure(evaluatedAtUtc, "invalid_path", "Path must start with '/' and stay within the dry-run size limit.");
            return false;
        }

        if (ContainsControl(path))
        {
            result = Failure(evaluatedAtUtc, "invalid_path", "Path must not contain control characters.");
            return false;
        }

        var query = NormalizeQuery(request.Query);
        if (query.Length > MaxInputLength || ContainsControl(query))
        {
            result = Failure(evaluatedAtUtc, "invalid_query", "Query must stay within the dry-run size limit and must not contain control characters.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.ClientIp) && !clientAddressSyntaxPolicy.IsIpLiteral(request.ClientIp))
        {
            result = Failure(evaluatedAtUtc, "invalid_client_ip", "ClientIp must be an IPv4 or IPv6 literal when supplied.");
            return false;
        }

        return true;
    }

    private static IProxyRouteDiagnosticsListener? SelectListener(
        IProxyRouteDiagnosticsConfigurationSnapshot snapshot,
        string? listenerName,
        string scheme,
        int? port,
        string? protocol)
    {
        var transport = scheme == "https" ? "https" : "http";
        IEnumerable<IProxyRouteDiagnosticsListener> listeners = snapshot.Listeners.Where(static listener => listener.Enabled);
        if (!string.IsNullOrWhiteSpace(listenerName))
        {
            listeners = listeners.Where(listener => string.Equals(listener.Name, listenerName, StringComparison.OrdinalIgnoreCase));
        }

        listeners = listeners.Where(listener => string.Equals(listener.Transport, transport, StringComparison.OrdinalIgnoreCase));
        if (port.HasValue)
        {
            listeners = listeners.Where(listener => listener.Port == port.Value);
        }

        listeners = protocol switch
        {
            "http1" => listeners.Where(static listener => listener.Protocols.HasFlag(RuntimeListenerProtocols.Http1)),
            "http2" => listeners.Where(static listener => listener.Protocols.HasFlag(RuntimeListenerProtocols.Http2)),
            "http3" => listeners.Where(static listener => listener.Http3EnabledForTraffic),
            _ => listeners
        };

        return listeners.FirstOrDefault();
    }

    private static IReadOnlyList<ProxyHeaderField> BuildHeaders(
        RouteMatchDryRunRequest request,
        List<RouteMatchDryRunFinding> findings)
    {
        List<ProxyHeaderField> headers = [new("Host", request.Host.Trim())];
        foreach (var header in request.Headers.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var name = header.Key?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name) || ContainsControl(name) || name.Contains(':', StringComparison.Ordinal))
            {
                findings.Add(new RouteMatchDryRunFinding("warning", "header_ignored", "A submitted header name was invalid and ignored."));
                continue;
            }

            var value = header.Value ?? "";
            if (ContainsControl(value))
            {
                findings.Add(new RouteMatchDryRunFinding("warning", "header_ignored", "A submitted header value contained control characters and was ignored."));
                continue;
            }

            if (SensitiveHeaderNames.Contains(name))
            {
                findings.Add(new RouteMatchDryRunFinding("info", "sensitive_header_redacted", $"Header '{name}' was considered for policy decisions but is not echoed in diagnostics."));
                value = "redacted";
            }

            headers.Add(new ProxyHeaderField(name, value));
        }

        return headers;
    }

    private static ProxyRouteDiagnosticsRequestFraming ResolveFraming(IReadOnlyList<ProxyHeaderField> headers)
    {
        foreach (var header in headers)
        {
            if (string.Equals(header.Name, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
                && header.Value.Contains("chunked", StringComparison.OrdinalIgnoreCase))
            {
                return ProxyRouteDiagnosticsRequestFraming.Chunked;
            }
        }

        foreach (var header in headers)
        {
            if (string.Equals(header.Name, "Content-Length", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(header.Value.Trim(), out var contentLength)
                && contentLength >= 0)
            {
                return ProxyRouteDiagnosticsRequestFraming.FromContentLength(contentLength);
            }
        }

        return ProxyRouteDiagnosticsRequestFraming.None;
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

    private static RouteMatchDryRunPolicy ExplainCache(
        IProxyRouteDiagnosticsRoute route,
        ProxyRouteDiagnosticsRequestHead requestHead,
        bool wouldProxy)
    {
        if (!route.CacheEnabled)
        {
            return DisabledPolicy("disabled");
        }

        if (!wouldProxy)
        {
            return new RouteMatchDryRunPolicy(true, false, "not_proxy_action");
        }

        if (!route.CacheMethods.Any(method => string.Equals(method, requestHead.Method, StringComparison.OrdinalIgnoreCase)))
        {
            return new RouteMatchDryRunPolicy(true, false, "method_not_cacheable");
        }

        if (requestHead.Framing.Kind != ProxyRouteDiagnosticsBodyKind.None)
        {
            return new RouteMatchDryRunPolicy(true, false, "request_body");
        }

        if (ContainsHeader(requestHead.Headers, "Authorization"))
        {
            return new RouteMatchDryRunPolicy(true, false, "authorization");
        }

        return new RouteMatchDryRunPolicy(true, true, "eligible_before_origin_response");
    }

    private static RouteMatchDryRunPolicy ExplainRetry(
        IProxyRouteDiagnosticsRoute route,
        ProxyRouteDiagnosticsRequestHead requestHead,
        bool wouldProxy)
    {
        if (!route.RetryEnabled)
        {
            return DisabledPolicy("disabled");
        }

        if (!wouldProxy)
        {
            return new RouteMatchDryRunPolicy(true, false, "not_proxy_action");
        }

        if (!route.RetryMethods.Any(method => string.Equals(method, requestHead.Method, StringComparison.OrdinalIgnoreCase)))
        {
            return new RouteMatchDryRunPolicy(true, false, "method_not_retryable");
        }

        if (requestHead.Framing.Kind != ProxyRouteDiagnosticsBodyKind.None)
        {
            return new RouteMatchDryRunPolicy(true, false, "request_body_not_replayable");
        }

        return new RouteMatchDryRunPolicy(true, true, "eligible_for_configured_transport_or_status_failures");
    }

    private static RouteMatchDryRunPolicy ExplainCircuitBreaker(IProxyRouteDiagnosticsRoute route, bool wouldProxy)
    {
        var enabled = route.Upstreams.Any(static upstream => upstream.CircuitBreakerEnabled);
        if (!enabled)
        {
            return DisabledPolicy("disabled");
        }

        return new RouteMatchDryRunPolicy(enabled, wouldProxy, wouldProxy ? "configured_for_one_or_more_upstreams" : "not_proxy_action");
    }

    private static bool ContainsHeader(IReadOnlyList<ProxyHeaderField> headers, string name)
    {
        return headers.Any(header => string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsUpgrade(IReadOnlyList<ProxyHeaderField> headers)
    {
        return headers.Any(static header => string.Equals(header.Name, "Upgrade", StringComparison.OrdinalIgnoreCase));
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

    private static RouteMatchDryRunPolicy DisabledPolicy(string reason)
    {
        return new RouteMatchDryRunPolicy(false, false, reason);
    }

    private static string NormalizeScheme(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "http" : value.Trim().ToLowerInvariant();
    }

    private static string NormalizeMethod(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "GET" : value.Trim().ToUpperInvariant();
    }

    private static string NormalizePath(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "/" : value.Trim();
    }

    private static string NormalizeQuery(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var query = value.Trim();
        return query.StartsWith('?') ? query : "?" + query;
    }

    private static string? NormalizeProtocol(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }

    private static bool ContainsControl(string value)
    {
        return value.Any(char.IsControl);
    }
}
