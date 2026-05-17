using System.Text;
using System.Text.Json;
using MDRAVA.API.Proxy.Configuration;
using MDRAVA.API.Proxy.Configuration.Loading;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Hosting;
using MDRAVA.API.Proxy.Http3;
using MDRAVA.API.Proxy.Metrics;
using Microsoft.Extensions.Options;
using YamlDotNet.Core;

namespace MDRAVA.API.Proxy.Diagnostics;

public sealed class ConfigLintService
{
    private const int MaxGeneratedBodyBytes = 64 * 1024;
    private readonly IProxyConfigurationStore _configurationStore;
    private readonly ProxyRuntimeState _runtimeState;
    private readonly SiteConfigurationParser _siteParser;
    private readonly IValidateOptions<ProxyOptions> _validator;
    private readonly ProxyMetrics _metrics;
    private readonly TimeProvider _timeProvider;
    private ConfigLintStatus _lastActiveStatus = ConfigLintStatus.Empty;

    public ConfigLintService(
        IProxyConfigurationStore configurationStore,
        ProxyRuntimeState runtimeState,
        SiteConfigurationParser siteParser,
        IValidateOptions<ProxyOptions> validator,
        ProxyMetrics metrics,
        TimeProvider timeProvider)
    {
        _configurationStore = configurationStore;
        _runtimeState = runtimeState;
        _siteParser = siteParser;
        _validator = validator;
        _metrics = metrics;
        _timeProvider = timeProvider;
    }

    public ConfigLintStatus LastActiveStatus => Volatile.Read(ref _lastActiveStatus);

    public ConfigLintResult LintActive()
    {
        var now = _timeProvider.GetUtcNow();
        if (!_configurationStore.TryGetSnapshot(out var snapshot) || snapshot is null)
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

        if (!SiteConfigurationFileDiscovery.TryParseFormat(request.Format, out var format))
        {
            return BuildResult(now, [Error("invalid_format", "Format must be 'json' or 'yaml'.", "lint-input", "format", "Set format to 'json', 'yaml', or 'yml'.")], []);
        }

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BuildResult(now, [Error("empty_config", "Submitted config text is required.", "lint-input", "text", "Submit one site configuration object.")], []);
        }

        SiteOptions? site;
        try
        {
            site = _siteParser.ReadSiteText(request.Text, format);
        }
        catch (JsonException exception)
        {
            return BuildResult(now, [Error("parse_error", $"JSON is invalid: {SafeMessage(exception.Message)}", "lint-input", null, "Fix the JSON syntax and retry linting.")], []);
        }
        catch (YamlException exception)
        {
            return BuildResult(now, [Error("parse_error", $"YAML is invalid: {SafeMessage(exception.Message)}", "lint-input", null, "Fix the YAML syntax and retry linting.")], []);
        }

        if (site is null)
        {
            return BuildResult(now, [Error("empty_config", "Submitted config did not contain a site object.", "lint-input", null, "Submit one site configuration object.")], []);
        }

        var options = SiteOptionsAggregator.ToProxyOptions([new SiteConfigurationSource("lint-input", site)]);
        var validation = _validator.Validate(null, options);
        var validationErrors = validation.Failed
            ? validation.Failures.Select(static failure => new ProxyConfigurationFileError("lint-input", failure)).ToArray()
            : [];

        var snapshot = ProxyConfigurationMapper.ToRuntimeSnapshot(
            options,
            new ProxyOperationalOptions(),
            new Dictionary<string, RuntimeCertificate>(StringComparer.OrdinalIgnoreCase),
            version: 0,
            loadedAtUtc: now,
            sourceDirectory: "submitted",
            sourceFiles: ["lint-input"],
            discovery: new ProxyConfigurationDiscovery(
                new ProxyFilesystemLayout("", "", "", "", "", "", ""),
                [new ProxyConfigurationFileDiscovery("lint-input", SiteConfigurationFileDiscovery.FormatName(format), "submitted", "Submitted lint input.")],
                [],
                []));

        List<ConfigLintFinding> findings = [];
        foreach (var error in validationErrors)
        {
            findings.Add(Error("validation_error", error.Message, SourceName(error.Path), null, "Fix the validation error before applying this config."));
        }

        findings.AddRange(Analyze(snapshot, activeRuntime: false, sourceName: "lint-input"));
        return BuildResult(now, findings, validationErrors);
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
        _metrics.ConfigLintRun(findings);
        return result;
    }

    private List<ConfigLintFinding> Analyze(
        ProxyConfigurationSnapshot snapshot,
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
        ProxyConfigurationSnapshot snapshot,
        bool activeRuntime,
        string? sourceName,
        List<ConfigLintFinding> findings)
    {
        var httpsListenerExists = snapshot.Listeners.Any(static listener => listener.Enabled && listener.Transport == RuntimeListenerTransport.Https);
        foreach (var group in snapshot.Listeners
            .Where(static listener => listener.Enabled)
            .GroupBy(static listener => $"{listener.Address}|{listener.Port}|{listener.Transport}", StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1))
        {
            findings.Add(Warning("overlapping_listener_bind", $"Multiple enabled listeners share bind identity {group.Key}.", sourceName, "listeners", "Keep only one enabled listener per address, port, and transport."));
        }

        IReadOnlyList<ProxyListenerStatus> runtimeListeners = activeRuntime ? _runtimeState.Snapshot().Listeners : [];
        foreach (var listener in snapshot.Listeners)
        {
            var path = $"listeners[{listener.Name}]";
            if (listener.Http3.Configured && !listener.Http3.EnabledForTraffic)
            {
                findings.Add(Warning("http3_configured_not_ready", $"Listener '{listener.Name}' has HTTP/3 configured but it is not ready for traffic: {listener.Http3.DisabledReason}.", sourceName, path, "Keep HTTP/3 disabled or satisfy the TLS and certificate requirements."));
            }

            if (listener.Http3.EnabledForTraffic && !Http3AltSvcPolicy.IsEnabled(listener))
            {
                findings.Add(Warning("http3_alt_svc_disabled", $"Listener '{listener.Name}' has HTTP/3 {listener.Http3.EnablementLevel} enabled but Alt-Svc advertisement is disabled.", sourceName, path, "Enable Http3AltSvcEnabled only after the QUIC listener is reachable."));
            }

            if (listener.Http3.EnabledForTraffic && listener.Http3MaxBufferedRequestBodyBytes > 0)
            {
                findings.Add(Info("http3_legacy_buffer_limit_configured", $"Listener '{listener.Name}' configures the legacy HTTP/3 buffered body limit, but request bodies are streamed.", sourceName, path, "Remove Http3MaxBufferedRequestBodyBytes unless a future compatibility phase reuses it."));
            }

            if (Http3AltSvcPolicy.IsEnabled(listener))
            {
                var ready = activeRuntime
                    && listener.QuicIdentity is not null
                    && runtimeListeners.Any(runtime => string.Equals(runtime.Kind, "quic", StringComparison.OrdinalIgnoreCase)
                        && runtime.State == ProxyListenerState.Active
                        && string.Equals(runtime.Identity, listener.QuicIdentity.Key, StringComparison.OrdinalIgnoreCase));
                if (!ready)
                {
                    findings.Add(Warning("http3_alt_svc_not_ready", $"Listener '{listener.Name}' configures Alt-Svc but no matching active QUIC listener is currently ready.", sourceName, path, "MDRAVA only emits Alt-Svc when the preview QUIC listener is active."));
                }
            }
        }

        foreach (var route in snapshot.Routes.Where(route => route.HttpsRedirect.Enabled && !httpsListenerExists))
        {
            findings.Add(Warning("https_redirect_without_https_listener", $"Route '{route.Name}' enables HTTP to HTTPS redirect but no enabled HTTPS listener exists.", sourceName, RoutePath(route), "Add an HTTPS listener or disable the redirect for this route."));
        }
    }

    private static void AddRouteFindings(
        ProxyConfigurationSnapshot snapshot,
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
            if (route.CanonicalHost.Enabled && HostEquals(route.Host, route.CanonicalHost.TargetHost))
            {
                findings.Add(Warning("canonical_host_loop", $"Route '{route.Name}' canonical host target equals its configured host.", sourceName, routePath, "Remove the canonical host policy or set a different target host."));
            }

            if (route.Cache.Enabled && LooksPrivate(route))
            {
                findings.Add(Warning("cache_private_path", $"Route '{route.Name}' enables cache on a path or header pattern that commonly serves private content.", sourceName, routePath, "Keep caching disabled for authenticated or user-specific resources."));
            }

            if (route.Retry.Enabled && route.Retry.RetryMethods.Any(static method => !IsSafeRetryMethod(method)))
            {
                findings.Add(Error("retry_unsafe_method", $"Route '{route.Name}' allows retry for an unsafe method.", sourceName, routePath, "Restrict retry methods to GET and HEAD."));
            }

            if (route.Upstreams.Any(static upstream => upstream.CircuitBreaker.Enabled)
                && (route.Upstreams.Count < 2 || !route.HealthCheck.Enabled))
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

                    if (!Http3RuntimeSupport.Project(snapshot.Listeners).QuicConnectionSupported)
                    {
                        findings.Add(Warning("upstream_http3_runtime_unavailable", $"Upstream '{upstream.Name}' uses HTTP/3 but this runtime does not report QUIC client support.", sourceName, upstreamPath, "Use HTTP/1.1 or HTTP/2 for this upstream on runtimes without System.Net.Quic client support."));
                    }

                    if (route.Retry.Enabled
                        && route.Retry.RetryMethods.Any(static method => !IsSafeRetryMethod(method)))
                    {
                        findings.Add(Warning("upstream_http3_retry_body_safety", $"Route '{route.Name}' combines HTTP/3 upstreams with retry methods beyond GET/HEAD.", sourceName, upstreamPath, "Keep HTTP/3 upstream retries limited to methods without request bodies unless replay is explicitly safe."));
                    }
                }

                if (string.Equals(upstream.Scheme, "https", StringComparison.OrdinalIgnoreCase)
                    && !upstream.Tls.ValidateCertificate)
                {
                    findings.Add(Warning("unsafe_upstream_tls_validation_disabled", $"Upstream '{upstream.Name}' disables platform TLS certificate validation.", sourceName, upstreamPath, "Use this only for local testing and restore certificate validation before production use."));
                }
            }

            if (route.Action == RuntimeRouteAction.StaticResponse
                && Encoding.UTF8.GetByteCount(route.StaticResponse.Body) >= MaxGeneratedBodyBytes * 4 / 5)
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
        ProxyConfigurationSnapshot snapshot,
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
        ProxyConfigurationSnapshot snapshot,
        string? sourceName,
        List<ConfigLintFinding> findings)
    {
        if (snapshot.Metrics.PublicMetricsEnabled)
        {
            findings.Add(Warning("metrics_public_exposure", "Public metrics exposure is configured.", sourceName, "metrics.publicMetricsEnabled", "Prefer the protected /admin/proxy/metrics endpoint."));
        }
    }

    private static bool RouteShadows(RuntimeRoute earlier, RuntimeRoute later)
    {
        return HostOverlaps(earlier.Host, later.Host)
            && later.PathPrefix.StartsWith(earlier.PathPrefix, StringComparison.Ordinal);
    }

    private static bool IsBroadCatchAll(RuntimeRoute route)
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

    private static bool LooksPrivate(RuntimeRoute route)
    {
        var path = route.PathPrefix.ToLowerInvariant();
        return path.Contains("admin", StringComparison.Ordinal)
            || path.Contains("auth", StringComparison.Ordinal)
            || path.Contains("account", StringComparison.Ordinal)
            || path.Contains("private", StringComparison.Ordinal)
            || path.Contains("profile", StringComparison.Ordinal)
            || path.Contains("user", StringComparison.Ordinal)
            || route.Cache.VaryByHeaders.Any(static header => string.Equals(header, "Authorization", StringComparison.OrdinalIgnoreCase)
                || string.Equals(header, "Cookie", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSafeRetryMethod(string method)
    {
        return string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase);
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

    private static string ActiveSource(ProxyConfigurationSnapshot snapshot)
    {
        return snapshot.SourceFiles.Count == 1
            ? SourceName(snapshot.SourceFiles[0]) ?? "active-config"
            : "active-config";
    }

    private static string? SourceName(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? null : Path.GetFileName(path);
    }

    private static string RoutePath(RuntimeRoute route)
    {
        return $"sites[{route.SiteName}].routes[{route.Name}]";
    }

    private static string SafeMessage(string message)
    {
        var sanitized = message.Replace('\r', ' ').Replace('\n', ' ');
        return sanitized.Length > 256 ? sanitized[..256] : sanitized;
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
