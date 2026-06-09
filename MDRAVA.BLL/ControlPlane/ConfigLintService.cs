using System.Text;

namespace MDRAVA.BLL.ControlPlane;

public sealed class ConfigLintService : IProxyConfigLintOperations
{
    private const int MaxGeneratedBodyBytes = 64 * 1024;
    private readonly IProxyConfigLintActiveConfigurationSource _activeConfigurationSource;
    private readonly IProxyConfigLintSubmittedConfigurationSource _submittedConfigurationSource;
    private readonly IProxyConfigLintRuntimeStateSource _runtimeStateSource;
    private readonly IProxyConfigLintMetricsSink _metricsSink;
    private readonly TimeProvider _timeProvider;
    private ConfigLintStatus _lastActiveStatus = ConfigLintStatus.Empty;

    public ConfigLintService(
        IProxyConfigLintActiveConfigurationSource activeConfigurationSource,
        IProxyConfigLintSubmittedConfigurationSource submittedConfigurationSource,
        IProxyConfigLintRuntimeStateSource runtimeStateSource,
        IProxyConfigLintMetricsSink metricsSink,
        TimeProvider timeProvider)
    {
        _activeConfigurationSource = activeConfigurationSource;
        _submittedConfigurationSource = submittedConfigurationSource;
        _runtimeStateSource = runtimeStateSource;
        _metricsSink = metricsSink;
        _timeProvider = timeProvider;
    }

    public ConfigLintStatus LastActiveStatus => Volatile.Read(ref _lastActiveStatus);

    public ConfigLintResult LintActive()
    {
        var now = _timeProvider.GetUtcNow();
        if (!_activeConfigurationSource.TryRead(out var snapshot) || snapshot is null)
        {
            var result = BuildResult(
                now,
                [Error("no_active_config", "No active proxy configuration is loaded.", null, null, "Load a valid config before linting the active runtime snapshot.")],
                []);
            StoreActiveStatus(result);
            return result;
        }

        var findings = Analyze(snapshot, activeRuntime: true, sourceName: ActiveSource(snapshot));
        var resultWithMetrics = BuildResult(now, findings, []);
        StoreActiveStatus(resultWithMetrics);
        return resultWithMetrics;
    }

    public ConfigLintResult LintSubmitted(ConfigLintRequest request)
    {
        var now = _timeProvider.GetUtcNow();
        if (request is null)
        {
            return BuildResult(now, [Error("missing_request", "A lint request body is required.", "lint-input", null, "Submit config text with an explicit format.")], []);
        }

        if (!TryParseFormat(request.Format, out var format))
        {
            return BuildResult(now, [Error("invalid_format", "Format must be 'json' or 'yaml'.", "lint-input", "format", "Set format to 'json', 'yaml', or 'yml'.")], []);
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BuildResult(now, [Error("empty_config", "Submitted config text is required.", "lint-input", "text", "Submit one site configuration object.")], []);
        }

        var submitted = _submittedConfigurationSource.Read(request, format, now);
        if (submitted.Failure is not null)
        {
            return BuildResult(now, [SubmittedFailure(submitted.Failure)], []);
        }

        if (submitted.Snapshot is null)
        {
            return BuildResult(now, [Error("empty_config", "Submitted config did not contain a site object.", "lint-input", null, "Submit one site configuration object.")], []);
        }

        List<ConfigLintFinding> findings = [];
        foreach (var error in submitted.ValidationErrors)
        {
            findings.Add(Error("validation_error", error.Message, SourceName(error.Path), null, "Fix the validation error before applying this config."));
        }

        findings.AddRange(Analyze(submitted.Snapshot, activeRuntime: false, sourceName: "lint-input"));
        return BuildResult(now, findings, submitted.ValidationErrors);
    }

    private void StoreActiveStatus(ConfigLintResult result)
    {
        Volatile.Write(ref _lastActiveStatus, new ConfigLintStatus(true, result.LintedAtUtc, result.Summary));
    }

    private ConfigLintResult BuildResult(
        DateTimeOffset lintedAtUtc,
        IReadOnlyList<ConfigLintFinding> findings,
        IReadOnlyList<ProxyConfigurationFileError> validationErrors)
    {
        var summary = new ConfigLintSummary(
            findings.Count(static finding => string.Equals(finding.Severity, "info", StringComparison.OrdinalIgnoreCase)),
            findings.Count(static finding => string.Equals(finding.Severity, "warning", StringComparison.OrdinalIgnoreCase)),
            findings.Count(static finding => string.Equals(finding.Severity, "error", StringComparison.OrdinalIgnoreCase)));
        var result = new ConfigLintResult(summary.Error == 0, lintedAtUtc, summary, findings, validationErrors);
        _metricsSink.ConfigLintRun(findings);
        return result;
    }

    private List<ConfigLintFinding> Analyze(
        ProxyConfigLintConfigurationSnapshot snapshot,
        bool activeRuntime,
        string? sourceName)
    {
        List<ConfigLintFinding> findings = [];
        AddListenerFindings(snapshot, activeRuntime, sourceName, findings);
        AddRouteFindings(snapshot, sourceName, findings);
        AddAdminFindings(snapshot, sourceName, findings);
        AddMetricsFindings(snapshot, sourceName, findings);
        return findings;
    }

    private void AddListenerFindings(
        ProxyConfigLintConfigurationSnapshot snapshot,
        bool activeRuntime,
        string? sourceName,
        List<ConfigLintFinding> findings)
    {
        var httpsListenerExists = snapshot.Listeners.Any(static listener =>
            listener.Enabled && string.Equals(listener.Transport, "Https", StringComparison.OrdinalIgnoreCase));
        foreach (var group in snapshot.Listeners
            .Where(static listener => listener.Enabled)
            .GroupBy(static listener => $"{listener.Address}|{listener.Port}|{listener.Transport}", StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1))
        {
            findings.Add(Warning("overlapping_listener_bind", $"Multiple enabled listeners share bind identity {group.Key}.", sourceName, "listeners", "Keep only one enabled listener per address, port, and transport."));
        }

        IReadOnlyList<ProxyListenerStatus> runtimeListeners = activeRuntime ? _runtimeStateSource.GetListeners() : [];
        foreach (var listener in snapshot.Listeners)
        {
            var path = $"listeners[{listener.Name}]";
            if (listener.Http3Configured && !listener.Http3EnabledForTraffic)
            {
                findings.Add(Warning("http3_configured_not_ready", $"Listener '{listener.Name}' has HTTP/3 configured but it is not ready for traffic: {listener.Http3DisabledReason}.", sourceName, path, "Keep HTTP/3 disabled or satisfy the TLS and certificate requirements."));
            }

            if (listener.Http3EnabledForTraffic && !listener.Http3AltSvcEnabled)
            {
                findings.Add(Warning("http3_alt_svc_disabled", $"Listener '{listener.Name}' has HTTP/3 {listener.Http3EnablementLevel} enabled but Alt-Svc advertisement is disabled.", sourceName, path, "Enable Http3AltSvcEnabled only after the QUIC listener is reachable."));
            }

            if (listener.Http3AltSvcEnabled)
            {
                var ready = activeRuntime
                    && listener.QuicIdentityKey is not null
                    && runtimeListeners.Any(runtime => string.Equals(runtime.Kind, "quic", StringComparison.OrdinalIgnoreCase)
                        && runtime.State == ProxyListenerState.Active
                        && string.Equals(runtime.Identity, listener.QuicIdentityKey, StringComparison.OrdinalIgnoreCase));
                if (!ready)
                {
                    findings.Add(Warning("http3_alt_svc_not_ready", $"Listener '{listener.Name}' configures Alt-Svc but no matching active QUIC listener is currently ready.", sourceName, path, "MDRAVA only emits Alt-Svc when the HTTP/3 QUIC listener is active."));
                }
            }
        }

        foreach (var route in snapshot.Routes.Where(route => route.HttpsRedirectEnabled && !httpsListenerExists))
        {
            findings.Add(Warning("https_redirect_without_https_listener", $"Route '{route.Name}' enables HTTP to HTTPS redirect but no enabled HTTPS listener exists.", sourceName, RoutePath(route), "Add an HTTPS listener or disable the redirect for this route."));
        }
    }

    private static void AddRouteFindings(
        ProxyConfigLintConfigurationSnapshot snapshot,
        string? sourceName,
        List<ConfigLintFinding> findings)
    {
        for (var laterIndex = 0; laterIndex < snapshot.Routes.Count; laterIndex++)
        {
            var later = snapshot.Routes[laterIndex];
            var shadowReported = false;
            var broadCatchAllReported = false;
            for (var earlierIndex = 0; earlierIndex < laterIndex; earlierIndex++)
            {
                var earlier = snapshot.Routes[earlierIndex];
                if (!shadowReported && RouteShadows(earlier, later))
                {
                    findings.Add(Warning("route_shadowed", $"Route '{later.Name}' is shadowed by earlier route '{earlier.Name}'.", sourceName, RoutePath(later), "Move the more specific route before the broad route or narrow the earlier path prefix."));
                    shadowReported = true;
                }

                if (!broadCatchAllReported && IsBroadCatchAll(earlier) && HostOverlaps(earlier.Host, later.Host))
                {
                    findings.Add(Warning("broad_catch_all_before_specific", $"Catch-all route '{earlier.Name}' appears before more specific route '{later.Name}'.", sourceName, RoutePath(earlier), "Put catch-all routes last."));
                    broadCatchAllReported = true;
                }

                if (shadowReported && broadCatchAllReported)
                {
                    break;
                }
            }
        }

        foreach (var group in snapshot.Routes.GroupBy(static route => $"{route.Host}|{route.PathPrefix}", StringComparer.OrdinalIgnoreCase))
        {
            if (group.Count() > 1)
            {
                findings.Add(Warning("overlapping_route_identity", $"Multiple routes use host/path identity '{group.Key}'.", sourceName, "routes", "Keep one route per host and path prefix or make ordering intentional."));
            }
        }

        foreach (var route in snapshot.Routes)
        {
            var routePath = RoutePath(route);
            if (route.CanonicalHostEnabled && HostEquals(route.Host, route.CanonicalHostTargetHost))
            {
                findings.Add(Warning("canonical_host_loop", $"Route '{route.Name}' canonical host target equals its configured host.", sourceName, routePath, "Remove the canonical host policy or set a different target host."));
            }

            if (route.CacheEnabled && LooksPrivate(route))
            {
                findings.Add(Warning("cache_private_path", $"Route '{route.Name}' enables cache on a path or header pattern that commonly serves private content.", sourceName, routePath, "Keep caching disabled for authenticated or user-specific resources."));
            }

            if (route.RetryEnabled && route.RetryMethods.Any(static method => !ProxyRequestMethodPolicy.IsSafeReadMethod(method)))
            {
                findings.Add(Error("retry_unsafe_method", $"Route '{route.Name}' allows retry for an unsafe method.", sourceName, routePath, "Restrict retry methods to GET and HEAD."));
            }

            if (route.Upstreams.Any(static upstream => upstream.CircuitBreakerEnabled)
                && (route.Upstreams.Count < 2 || !route.HealthCheckEnabled))
            {
                findings.Add(Warning("circuit_breaker_low_redundancy", $"Route '{route.Name}' configures a circuit breaker without multiple upstreams or active health checks.", sourceName, routePath, "Circuit breakers are most useful with redundant upstreams and health checks."));
            }

            foreach (var upstream in route.Upstreams)
            {
                var upstreamPath = $"{routePath}.upstreams[{upstream.Name}]";
                if (string.Equals(upstream.Protocol, RuntimeUpstreamProtocol.Http2, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(upstream.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                {
                    findings.Add(Error("upstream_http2_without_https", $"Upstream '{upstream.Name}' uses HTTP/2 without HTTPS.", sourceName, upstreamPath, "Set scheme to https or use upstream protocol http1."));
                }

                if (string.Equals(upstream.Protocol, RuntimeUpstreamProtocol.Http3, StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.Equals(upstream.Scheme, "https", StringComparison.OrdinalIgnoreCase))
                    {
                        findings.Add(Error("upstream_http3_without_https", $"Upstream '{upstream.Name}' uses HTTP/3 without HTTPS.", sourceName, upstreamPath, "Set scheme to https because h3c is not supported."));
                    }

                    if (!snapshot.Http3QuicConnectionSupported)
                    {
                        findings.Add(Warning("upstream_http3_runtime_unavailable", $"Upstream '{upstream.Name}' uses HTTP/3 but this runtime does not report QUIC client support.", sourceName, upstreamPath, "Use HTTP/1.1 or HTTP/2 for this upstream on runtimes without System.Net.Quic client support."));
                    }

                    if (route.RetryEnabled
                        && route.RetryMethods.Any(static method => !ProxyRequestMethodPolicy.IsSafeReadMethod(method)))
                    {
                        findings.Add(Warning("upstream_http3_retry_body_safety", $"Route '{route.Name}' combines HTTP/3 upstreams with retry methods beyond GET/HEAD.", sourceName, upstreamPath, "Keep HTTP/3 upstream retries limited to methods without request bodies unless replay is explicitly safe."));
                    }
                }

                if (string.Equals(upstream.Scheme, "https", StringComparison.OrdinalIgnoreCase)
                    && !upstream.TlsValidateCertificate)
                {
                    findings.Add(Warning("unsafe_upstream_tls_validation_disabled", $"Upstream '{upstream.Name}' disables platform TLS certificate validation.", sourceName, upstreamPath, "Use this only for local testing and restore certificate validation before production use."));
                }
            }

            if (string.Equals(route.Action, "StaticResponse", StringComparison.Ordinal)
                && Encoding.UTF8.GetByteCount(route.StaticResponseBody) >= MaxGeneratedBodyBytes * 4 / 5)
            {
                findings.Add(Warning("static_response_body_near_limit", $"Static response route '{route.Name}' has a body near the generated-response size limit.", sourceName, routePath, "Move larger content behind an upstream application or keep the static body small."));
            }
        }

        foreach (var group in snapshot.Routes.GroupBy(static route => route.SiteName, StringComparer.OrdinalIgnoreCase))
        {
            if (!group.Any(static route => route.PathPrefix == "/"))
            {
                findings.Add(Info("site_without_fallback_route", $"Site '{group.Key}' has no '/' fallback route.", sourceName, $"sites[{group.Key}]", "Add an explicit fallback route if unmatched paths should have controlled behavior."));
            }
        }
    }

    private static void AddAdminFindings(
        ProxyConfigLintConfigurationSnapshot snapshot,
        string? sourceName,
        List<ConfigLintFinding> findings)
    {
        foreach (var url in snapshot.AdminSecurity.Urls)
        {
            if (!IsNonLocalAdminUrl(url))
            {
                continue;
            }

            var severity = snapshot.AdminSecurity.RequireAuthentication ? "warning" : "error";
            findings.Add(new ConfigLintFinding(
                severity,
                snapshot.AdminSecurity.RequireAuthentication ? "admin_nonlocal_bind" : "admin_nonlocal_bind_without_auth",
                snapshot.AdminSecurity.RequireAuthentication
                    ? "Admin API is configured on a non-local bind address and relies on bearer-token authentication."
                    : "Admin API is configured on a non-local bind address without authentication.",
                sourceName,
                "admin.urls",
                "Keep admin binding localhost-only unless remote administration is intentional and authenticated."));
        }
    }

    private static void AddMetricsFindings(
        ProxyConfigLintConfigurationSnapshot snapshot,
        string? sourceName,
        List<ConfigLintFinding> findings)
    {
        if (snapshot.Metrics.PublicMetricsEnabled)
        {
            findings.Add(Warning("metrics_public_exposure", "Public metrics exposure is configured.", sourceName, "metrics.publicMetricsEnabled", "Prefer the protected /admin/proxy/metrics endpoint."));
        }
    }

    private static ConfigLintFinding SubmittedFailure(ProxyConfigLintSubmittedConfigurationFailure failure)
    {
        return failure.Kind switch
        {
            ProxyConfigLintSubmittedConfigurationFailureKind.JsonParseError => Error("parse_error", $"JSON is invalid: {SafeMessage(failure.Message ?? "")}", "lint-input", null, "Fix the JSON syntax and retry linting."),
            ProxyConfigLintSubmittedConfigurationFailureKind.YamlParseError => Error("parse_error", $"YAML is invalid: {SafeMessage(failure.Message ?? "")}", "lint-input", null, "Fix the YAML syntax and retry linting."),
            _ => Error("empty_config", "Submitted config did not contain a site object.", "lint-input", null, "Submit one site configuration object.")
        };
    }

    private static bool RouteShadows(ProxyConfigLintRoute earlier, ProxyConfigLintRoute later)
    {
        return HostOverlaps(earlier.Host, later.Host)
            && later.PathPrefix.StartsWith(earlier.PathPrefix, StringComparison.Ordinal);
    }

    private static bool IsBroadCatchAll(ProxyConfigLintRoute route)
    {
        return string.Equals(route.Host, "*", StringComparison.Ordinal)
            && string.Equals(route.PathPrefix, "/", StringComparison.Ordinal);
    }

    private static bool HostOverlaps(string earlierHost, string laterHost)
    {
        return string.Equals(earlierHost, "*", StringComparison.Ordinal)
            || string.Equals(laterHost, "*", StringComparison.Ordinal)
            || HostEquals(earlierHost, laterHost);
    }

    private static bool HostEquals(string left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return string.Equals(StripPort(left), StripPort(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string StripPort(string host)
    {
        var colonIndex = host.LastIndexOf(':');
        return colonIndex <= 0 || host.Contains(']', StringComparison.Ordinal)
            ? host
            : host[..colonIndex];
    }

    private static bool LooksPrivate(ProxyConfigLintRoute route)
    {
        var path = route.PathPrefix.ToLowerInvariant();
        return path.Contains("admin", StringComparison.Ordinal)
            || path.Contains("auth", StringComparison.Ordinal)
            || path.Contains("account", StringComparison.Ordinal)
            || path.Contains("private", StringComparison.Ordinal)
            || path.Contains("profile", StringComparison.Ordinal)
            || path.Contains("user", StringComparison.Ordinal)
            || route.CacheVaryByHeaders.Any(static header => string.Equals(header, "Authorization", StringComparison.OrdinalIgnoreCase)
                || string.Equals(header, "Cookie", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsNonLocalAdminUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!System.Net.IPAddress.TryParse(uri.Host, out var address))
        {
            return true;
        }

        return !System.Net.IPAddress.IsLoopback(address);
    }

    private static string ActiveSource(ProxyConfigLintConfigurationSnapshot snapshot)
    {
        return snapshot.SourceFiles.Count == 1
            ? SourceName(snapshot.SourceFiles[0]) ?? "active-config"
            : "active-config";
    }

    private static string? SourceName(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : Path.GetFileName(path);
    }

    private static string RoutePath(ProxyConfigLintRoute route)
    {
        return $"sites[{route.SiteName}].routes[{route.Name}]";
    }

    private static string SafeMessage(string message)
    {
        var sanitized = message.Replace('\r', ' ').Replace('\n', ' ');
        return sanitized.Length > 256 ? sanitized[..256] : sanitized;
    }

    private static bool TryParseFormat(
        string format,
        out ProxyConfigurationNormalizeFormat parsed)
    {
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            parsed = ProxyConfigurationNormalizeFormat.Json;
            return true;
        }

        if (string.Equals(format, "yaml", StringComparison.OrdinalIgnoreCase)
            || string.Equals(format, "yml", StringComparison.OrdinalIgnoreCase))
        {
            parsed = ProxyConfigurationNormalizeFormat.Yaml;
            return true;
        }

        parsed = ProxyConfigurationNormalizeFormat.Json;
        return false;
    }

    private static ConfigLintFinding Info(
        string code,
        string message,
        string? source,
        string? path,
        string? suggestedFix)
    {
        return new ConfigLintFinding("info", code, message, source, path, suggestedFix);
    }

    private static ConfigLintFinding Warning(
        string code,
        string message,
        string? source,
        string? path,
        string? suggestedFix)
    {
        return new ConfigLintFinding("warning", code, message, source, path, suggestedFix);
    }

    private static ConfigLintFinding Error(
        string code,
        string message,
        string? source,
        string? path,
        string? suggestedFix)
    {
        return new ConfigLintFinding("error", code, message, source, path, suggestedFix);
    }
}
