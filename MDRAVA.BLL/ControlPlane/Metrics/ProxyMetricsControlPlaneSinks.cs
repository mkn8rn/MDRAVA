using MDRAVA.BLL.ControlPlane.ConfigLint;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class ProxyMetrics
{
    public void ConfigReloadSucceeded() => Interlocked.Increment(ref _configReloadSuccesses);

    public void ConfigReloadFailed() => Interlocked.Increment(ref _configReloadFailures);

    public void AdminAuthSucceeded() => Interlocked.Increment(ref _adminAuthSuccesses);

    public void AdminAuthFailed() => Interlocked.Increment(ref _adminAuthFailures);

    public void AcmeRenewalAttempted() => Interlocked.Increment(ref _acmeRenewalAttempts);

    public void AcmeRenewalSucceeded() => Interlocked.Increment(ref _acmeRenewalSuccesses);

    public void AcmeRenewalFailed() => Interlocked.Increment(ref _acmeRenewalFailures);

    public void RetryAttempted() => Interlocked.Increment(ref _retryAttempts);

    public void RetryExhausted() => Interlocked.Increment(ref _retryExhausted);

    public void RetrySkipped(string reason)
    {
        var counter = _retrySkippedByReason.GetOrAdd(ProxyMetricLabelPolicy.NormalizeValue(reason), static _ => new RequestSeriesCounter());
        Interlocked.Increment(ref counter.Count);
    }

    public void CircuitOpened() => Interlocked.Increment(ref _circuitOpened);

    public void CircuitHalfOpened() => Interlocked.Increment(ref _circuitHalfOpened);

    public void CircuitClosed() => Interlocked.Increment(ref _circuitClosed);

    public void CircuitRejected() => Interlocked.Increment(ref _circuitRejections);

    public void ListenerReloadAttempted() => Interlocked.Increment(ref _listenerReloadAttempts);

    public void ListenerReloadSucceeded(int added, int removed, int changed, int unchanged)
    {
        Interlocked.Increment(ref _listenerReloadSuccesses);
        Interlocked.Add(ref _listenerReloadAdded, added);
        Interlocked.Add(ref _listenerReloadRemoved, removed);
        Interlocked.Add(ref _listenerReloadChanged, changed);
        Interlocked.Add(ref _listenerReloadUnchanged, unchanged);
    }

    public void ListenerReloadFailed() => Interlocked.Increment(ref _listenerReloadFailures);

    public void ListenerStartFailed() => Interlocked.Increment(ref _listenerStartFailures);

    public void ListenerDrained() => Interlocked.Increment(ref _listenerDrainCount);

    public void SetActiveListeners(long count) => Interlocked.Exchange(ref _activeListeners, count);

    public void ConfigLintRun(IReadOnlyList<ConfigLintFinding> findings)
    {
        Interlocked.Increment(ref _configLintRuns);
        foreach (var finding in findings)
        {
            var key = new ConfigLintFindingKey(
                ProxyMetricLabelPolicy.NormalizeValue(finding.Severity),
                ProxyMetricLabelPolicy.NormalizeValue(finding.Code));
            var counter = _configLintFindings.GetOrAdd(key, static _ => new RequestSeriesCounter());
            Interlocked.Increment(ref counter.Count);
        }
    }

    public void RouteMatchDryRun(string? failureReason)
    {
        Interlocked.Increment(ref _routeMatchDryRuns);
        if (string.IsNullOrWhiteSpace(failureReason))
        {
            return;
        }

        var counter = _routeMatchDryRunFailures.GetOrAdd(ProxyMetricLabelPolicy.NormalizeValue(failureReason), static _ => new RequestSeriesCounter());
        Interlocked.Increment(ref counter.Count);
    }
}
