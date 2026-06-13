using MDRAVA.BLL.ControlPlane.ConfigLint;
using MDRAVA.BLL.ControlPlane.Forwarding;
using MDRAVA.BLL.ControlPlane.RouteDiagnostics;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class ProxyMetrics
{
    private IReadOnlyDictionary<string, long> ReadRequestFailuresByKind()
    {
        Dictionary<string, long> failuresByKind = new(StringComparer.Ordinal);
        foreach (var failureKind in FailureKinds)
        {
            if (failureKind == ProxyFailureKind.None)
            {
                continue;
            }

            failuresByKind[failureKind.ToString()] = Interlocked.Read(ref _requestFailuresByKind[(int)failureKind]);
        }

        return failuresByKind;
    }

    private ProxyRequestSeriesSnapshot[] ReadRequestsByRoute()
    {
        return _requestsByRoute
            .Select(static pair => new ProxyRequestSeriesSnapshot(
                pair.Key.Site,
                pair.Key.Route,
                pair.Key.Action,
                pair.Key.StatusClass,
                Interlocked.Read(ref pair.Value.Count)))
            .OrderBy(static item => item.Site, StringComparer.Ordinal)
            .ThenBy(static item => item.Route, StringComparer.Ordinal)
            .ThenBy(static item => item.Action, StringComparer.Ordinal)
            .ThenBy(static item => item.StatusClass, StringComparer.Ordinal)
            .ToArray();
    }

    private ProxyRetrySkippedSnapshot[] ReadRetrySkipped()
    {
        return _retrySkippedByReason
            .Select(static pair => new ProxyRetrySkippedSnapshot(pair.Key, Interlocked.Read(ref pair.Value.Count)))
            .OrderBy(static item => item.Reason, StringComparer.Ordinal)
            .ToArray();
    }

    private ProxyUpstreamSelectionSnapshot[] ReadUpstreamSelectionsByUpstream()
    {
        return _upstreamSelectionsByUpstream
            .Select(static pair => new ProxyUpstreamSelectionSnapshot(
                pair.Key.Route,
                pair.Key.Upstream,
                pair.Key.Scheme,
                pair.Key.Protocol,
                Interlocked.Read(ref pair.Value.Count)))
            .OrderBy(static item => item.Route, StringComparer.Ordinal)
            .ThenBy(static item => item.Upstream, StringComparer.Ordinal)
            .ThenBy(static item => item.Scheme, StringComparer.Ordinal)
            .ToArray();
    }

    private IReadOnlyDictionary<string, long> ReadHttp2ProtocolErrors()
    {
        return _http2ProtocolErrors
            .ToDictionary(static pair => pair.Key, static pair => Interlocked.Read(ref pair.Value.Count), StringComparer.Ordinal);
    }

    private IReadOnlyDictionary<string, long> ReadUpstreamHttp3ProtocolErrors()
    {
        return _upstreamHttp3ProtocolErrors
            .ToDictionary(static pair => pair.Key, static pair => Interlocked.Read(ref pair.Value.Count), StringComparer.Ordinal);
    }

    private ProxyHttp3RequestOutcomeSnapshot[] ReadHttp3RequestsByOutcome()
    {
        return _http3RequestsByOutcome
            .Select(static pair => new ProxyHttp3RequestOutcomeSnapshot(
                pair.Key.Method,
                pair.Key.Outcome,
                pair.Key.StatusClass,
                Interlocked.Read(ref pair.Value.Count)))
            .OrderBy(static item => item.Method, StringComparer.Ordinal)
            .ThenBy(static item => item.Outcome, StringComparer.Ordinal)
            .ThenBy(static item => item.StatusClass, StringComparer.Ordinal)
            .ToArray();
    }

    private IReadOnlyDictionary<string, long> ReadHttp3RejectedRequests()
    {
        return _http3RejectedRequests
            .ToDictionary(static pair => pair.Key, static pair => Interlocked.Read(ref pair.Value.Count), StringComparer.Ordinal);
    }

    private IReadOnlyDictionary<string, long> ReadHttp3ProtocolErrors()
    {
        return _http3ProtocolErrors
            .ToDictionary(static pair => pair.Key, static pair => Interlocked.Read(ref pair.Value.Count), StringComparer.Ordinal);
    }

    private ProxyConfigLintFindingMetricSnapshot[] ReadConfigLintFindings()
    {
        return _configLintFindings
            .Select(static pair => new ProxyConfigLintFindingMetricSnapshot(
                pair.Key.Severity,
                pair.Key.Code,
                Interlocked.Read(ref pair.Value.Count)))
            .OrderBy(static item => item.Severity, StringComparer.Ordinal)
            .ThenBy(static item => item.Code, StringComparer.Ordinal)
            .ToArray();
    }

    private ProxyRouteDryRunFailureSnapshot[] ReadRouteMatchDryRunFailures()
    {
        return _routeMatchDryRunFailures
            .Select(static pair => new ProxyRouteDryRunFailureSnapshot(pair.Key, Interlocked.Read(ref pair.Value.Count)))
            .OrderBy(static item => item.Reason, StringComparer.Ordinal)
            .ToArray();
    }
}
