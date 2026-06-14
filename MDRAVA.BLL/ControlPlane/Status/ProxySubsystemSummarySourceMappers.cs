using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.Listeners;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.BLL.ControlPlane.Status;

public static class ProxyRuntimeListenerSummarySourceMapper
{
    public static IReadOnlyList<ProxyRuntimeListenerSummarySource> FromRuntimeSummary(
        ProxyStatusRuntimeSummary runtime)
    {
        return runtime.Listeners
            .Select(static listener => new ProxyRuntimeListenerSummarySource(
                string.Equals(listener.Kind, "quic", StringComparison.OrdinalIgnoreCase),
                listener.State))
            .ToArray();
    }
}

public static class ProxyUpstreamSummarySourceMapper
{
    public static IReadOnlyList<ProxyUpstreamSummarySource> FromStatusResponses(
        IReadOnlyList<ProxyUpstreamStatus> upstreams)
    {
        return upstreams
            .Select(static upstream => new ProxyUpstreamSummarySource(
                upstream.HealthState,
                upstream.HealthCheckEnabled,
                upstream.CircuitBreaker.Enabled,
                upstream.CircuitBreaker.State))
            .ToArray();
    }
}

public static class ProxyLimitSummarySourceMapper
{
    public static ProxyLimitRuntimeSummarySource FromMetrics(ProxyMetricsSnapshot metrics)
    {
        return new ProxyLimitRuntimeSummarySource(
            metrics.ClientConnections.Active,
            metrics.Tls.ActiveHandshakes,
            metrics.Http2.ActiveStreams,
            metrics.Http3.ActiveStreams,
            metrics.UpstreamHttp3.ActiveStreams);
    }
}

public static class ProxyLogSummarySourceMapper
{
    public static ProxyLogSummarySource FromStatus(ProxyLogPersistenceStatus logPersistence)
    {
        return new ProxyLogSummarySource(
            logPersistence.AccessLogEnabled,
            logPersistence.AdminAuditEnabled,
            logPersistence.State,
            logPersistence.Reason);
    }
}

public static class ProxyShutdownSummarySourceMapper
{
    public static ProxyShutdownSummarySource FromRuntimeSummary(ProxyStatusRuntimeSummary runtime)
    {
        return new ProxyShutdownSummarySource(
            runtime.ListenerLive,
            runtime.IsShuttingDown,
            runtime.ShutdownStartedAtUtc,
            runtime.ShutdownDeadlineUtc);
    }
}
