using MDRAVA.BLL.Configuration;

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
        if (!ConfigLintSubmittedRequestReader.TryRead(request, out var input, out var requestFailure))
        {
            return BuildResult(now, [requestFailure!], []);
        }

        var submitted = _submittedConfigurationSource.Read(input!.Text, input.Format, now);
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
        var runtimeListeners = activeRuntime ? _runtimeStateSource.GetListenerStates() : [];
        findings.AddRange(ConfigLintListenerAnalyzer.Analyze(
            snapshot,
            activeRuntime,
            runtimeListeners,
            sourceName));
        findings.AddRange(ConfigLintRouteAnalyzer.Analyze(snapshot, sourceName));
        findings.AddRange(ConfigLintExposureAnalyzer.Analyze(snapshot, _adminUrlPolicy, sourceName));
        return findings;
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

    private static string SafeMessage(string message)
    {
        var sanitized = message.Replace('\r', ' ').Replace('\n', ' ');
        return sanitized.Length > 256 ? sanitized[..256] : sanitized;
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
