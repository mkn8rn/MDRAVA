using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public sealed class ConfigLintService : IProxyConfigLintOperations
{
    private readonly IProxyConfigLintActiveConfigurationSource _activeConfigurationSource;
    private readonly IProxyConfigLintSubmittedConfigurationSource _submittedConfigurationSource;
    private readonly IProxyConfigLintRuntimeStateSource _runtimeStateSource;
    private readonly IProxyConfigLintMetricsSink _metricsSink;
    private readonly IProxyConfigLintSourceNameFormatter _sourceNameFormatter;
    private readonly IProxyAdminUrlPolicy _adminUrlPolicy;
    private readonly TimeProvider _timeProvider;
    private ConfigLintStatus _lastActiveStatus = ConfigLintStatus.Empty;

    public ConfigLintService(
        IProxyConfigLintActiveConfigurationSource activeConfigurationSource,
        IProxyConfigLintSubmittedConfigurationSource submittedConfigurationSource,
        IProxyConfigLintRuntimeStateSource runtimeStateSource,
        IProxyConfigLintMetricsSink metricsSink,
        IProxyConfigLintSourceNameFormatter sourceNameFormatter,
        IProxyAdminUrlPolicy adminUrlPolicy,
        TimeProvider timeProvider)
    {
        _activeConfigurationSource = activeConfigurationSource;
        _submittedConfigurationSource = submittedConfigurationSource;
        _runtimeStateSource = runtimeStateSource;
        _metricsSink = metricsSink;
        _sourceNameFormatter = sourceNameFormatter;
        _adminUrlPolicy = adminUrlPolicy;
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

    public ConfigLintResult LintSubmitted(ConfigLintRequest? request)
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

        var submitted = _submittedConfigurationSource.Read(request.Text, format, now);
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
        findings.AddRange(ConfigLintRouteAnalyzer.Analyze(snapshot, sourceName));
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

        IReadOnlyList<ProxyConfigLintRuntimeListenerState> runtimeListeners = activeRuntime ? _runtimeStateSource.GetListenerStates() : [];
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
                        && runtime.Active
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

    private void AddAdminFindings(
        ProxyConfigLintConfigurationSnapshot snapshot,
        string? sourceName,
        List<ConfigLintFinding> findings)
    {
        foreach (var url in snapshot.AdminSecurity.Urls)
        {
            if (!_adminUrlPolicy.IsNonLocal(url))
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

    private string ActiveSource(ProxyConfigLintConfigurationSnapshot snapshot)
    {
        return snapshot.SourceFiles.Count == 1
            ? SourceName(snapshot.SourceFiles[0]) ?? "active-config"
            : "active-config";
    }

    private string? SourceName(string? path)
    {
        return _sourceNameFormatter.FormatSourceName(path);
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
        string? format,
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
