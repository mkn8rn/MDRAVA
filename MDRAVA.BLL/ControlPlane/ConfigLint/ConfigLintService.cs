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
                [ConfigLintServiceFailureFindingFactory.NoActiveConfig()],
                []);
            StoreActiveStatus(result);
            return result;
        }

        var findings = Analyze(
            snapshot,
            activeRuntime: true,
            sourceName: ConfigLintSourceNameResolver.ActiveSource(snapshot, _sourceNameFormatter));
        var resultWithMetrics = BuildResult(now, findings, []);
        StoreActiveStatus(resultWithMetrics);
        return resultWithMetrics;
    }

    public ConfigLintResult LintSubmitted(ConfigLintRequest? request)
    {
        var now = _timeProvider.GetUtcNow();
        var requestDecision = ConfigLintSubmittedRequestReader.Read(request);
        if (requestDecision is ConfigLintSubmittedRequestDecision.RejectedDecision rejectedRequest)
        {
            return BuildResult(now, [rejectedRequest.Failure], []);
        }

        var input = ((ConfigLintSubmittedRequestDecision.AcceptedDecision)requestDecision).Input;
        var submitted = _submittedConfigurationSource.Read(input.Text, input.Format, now);
        if (submitted.Failure is not null)
        {
            return BuildResult(now, [ConfigLintSubmittedFailureMapper.ToFinding(submitted.Failure)], []);
        }

        if (submitted.Snapshot is null)
        {
            return BuildResult(now, [ConfigLintServiceFailureFindingFactory.EmptySubmittedConfig()], []);
        }

        List<ConfigLintFinding> findings = [.. ConfigLintValidationErrorMapper.ToFindings(
            submitted.ValidationErrors,
            _sourceNameFormatter)];

        findings.AddRange(Analyze(submitted.Snapshot, activeRuntime: false, sourceName: "lint-input"));
        return BuildResult(now, findings, submitted.ValidationErrors);
    }

    private void StoreActiveStatus(ConfigLintResult result)
    {
        Volatile.Write(ref _lastActiveStatus, ConfigLintStatus.Completed(result.LintedAtUtc, result.Summary));
    }

    private ConfigLintResult BuildResult(
        DateTimeOffset lintedAtUtc,
        IReadOnlyList<ConfigLintFinding> findings,
        IReadOnlyList<ProxyConfigurationFileError> validationErrors)
    {
        var result = ConfigLintResult.Completed(lintedAtUtc, findings, validationErrors);
        _metricsSink.ConfigLintRun(findings);
        return result;
    }

    private IReadOnlyList<ConfigLintFinding> Analyze(
        ProxyConfigLintConfigurationSnapshot snapshot,
        bool activeRuntime,
        string? sourceName)
    {
        var runtimeListeners = activeRuntime ? _runtimeStateSource.GetListenerStates() : [];
        return ConfigLintConfigurationAnalyzer.Analyze(
            snapshot,
            activeRuntime,
            runtimeListeners,
            _adminUrlPolicy,
            sourceName);
    }

}
