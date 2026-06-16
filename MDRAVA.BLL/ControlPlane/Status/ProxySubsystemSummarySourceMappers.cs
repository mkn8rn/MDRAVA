using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.Listeners;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.BLL.ControlPlane.Status;

public static class ProxyRuntimeListenerSummarySourceMapper
{
    public static IReadOnlyList<ProxyRuntimeListenerSummarySource> FromSources(
        IEnumerable<ProxyListenerStatus> listeners)
    {
        ArgumentNullException.ThrowIfNull(listeners);

        return listeners
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
        ArgumentNullException.ThrowIfNull(upstreams);

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
    public static ProxyLimitRuntimeSummarySource FromSources(
        long activeConnections,
        long activeTlsHandshakes,
        long activeHttp2Streams,
        long activeHttp3Streams,
        long activeUpstreamHttp3Streams)
    {
        return new ProxyLimitRuntimeSummarySource(
            activeConnections,
            activeTlsHandshakes,
            activeHttp2Streams,
            activeHttp3Streams,
            activeUpstreamHttp3Streams);
    }
}

public static class ProxyLogSummarySourceMapper
{
    public static ProxyLogSummarySource FromStatus(ProxyLogPersistenceStatus logPersistence)
    {
        ArgumentNullException.ThrowIfNull(logPersistence);

        return new ProxyLogSummarySource(
            logPersistence.AccessLogEnabled,
            logPersistence.AdminAuditEnabled,
            logPersistence.State,
            logPersistence.Reason);
    }
}

public static class ProxyShutdownSummarySourceMapper
{
    public static ProxyShutdownSummarySource FromSources(
        bool isRunning,
        bool isShuttingDown,
        DateTimeOffset? shutdownStartedAtUtc,
        DateTimeOffset? shutdownDeadlineUtc)
    {
        return new ProxyShutdownSummarySource(
            isRunning,
            isShuttingDown,
            shutdownStartedAtUtc,
            shutdownDeadlineUtc);
    }
}
