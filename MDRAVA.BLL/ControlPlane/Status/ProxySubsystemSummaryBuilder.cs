using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Listeners;

namespace MDRAVA.BLL.ControlPlane.Status;

public static partial class ProxySubsystemSummaryBuilder
{
    public static ProxyConfigSubsystemSummary BuildConfig(
        bool active,
        int? generation,
        DateTimeOffset? loadedAtUtc,
        bool? lastListenerReloadSucceeded)
    {
        return new ProxyConfigSubsystemSummary(
            active,
            generation,
            loadedAtUtc,
            lastListenerReloadSucceeded,
            lastListenerReloadSucceeded is null
                ? null
                : lastListenerReloadSucceeded.Value ? "listener_reload_succeeded" : "listener_reload_failed");
    }

    public static ProxyListenerSubsystemSummary BuildListeners(
        IReadOnlyList<ProxyConfiguredListenerSummarySource> configuredListeners,
        IReadOnlyList<ProxyRuntimeListenerSummarySource> runtimeListeners)
    {
        return new ProxyListenerSubsystemSummary(
            configuredListeners.Count,
            configuredListeners.Count(static listener => listener.Enabled),
            runtimeListeners.Count(static listener => listener.State == ProxyListenerState.Active),
            runtimeListeners.Count(static listener => listener.State == ProxyListenerState.Failed),
            runtimeListeners.Count(static listener => listener.State == ProxyListenerState.Draining),
            configuredListeners.Count(static listener => listener.Enabled && listener.Http1Enabled),
            configuredListeners.Count(static listener => listener.Enabled && listener.Http2Enabled),
            configuredListeners.Count(static listener => listener.Enabled && listener.Http3EnabledForTraffic),
            runtimeListeners.Count(static listener => listener.IsQuic && listener.State == ProxyListenerState.Active));
    }

    public static ProxyRouteSubsystemSummary BuildRoutes(IReadOnlyList<ProxyRouteSummarySource> routes)
    {
        return new ProxyRouteSubsystemSummary(
            routes.Select(static route => route.SiteName).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            routes.Count,
            routes.Count(static route => route.IsProxyRoute),
            routes.Count(static route => !route.IsProxyRoute),
            routes.Count(static route => route.CacheEnabled));
    }

    public static ProxyUpstreamSubsystemSummary BuildUpstreams(IReadOnlyList<ProxyUpstreamSummarySource> upstreams)
    {
        return new ProxyUpstreamSubsystemSummary(
            upstreams.Count,
            upstreams.Count(static upstream => upstream.HealthState == UpstreamHealthState.Healthy),
            upstreams.Count(static upstream => upstream.HealthState == UpstreamHealthState.Unhealthy),
            upstreams.Count(static upstream => upstream.HealthState == UpstreamHealthState.Unknown),
            upstreams.Count(static upstream => upstream.HealthCheckEnabled));
    }

    public static ProxyCacheSubsystemSummary BuildCache(
        int enabledRoutes,
        ProxyCacheStatus? cacheStatus)
    {
        return new ProxyCacheSubsystemSummary(
            enabledRoutes > 0,
            enabledRoutes,
            cacheStatus?.EntryCount ?? 0,
            cacheStatus?.ApproximateBytes ?? 0);
    }

    public static ProxyCircuitSubsystemSummary BuildCircuits(IReadOnlyList<ProxyUpstreamSummarySource> upstreams)
    {
        return new ProxyCircuitSubsystemSummary(
            upstreams.Count(static upstream => upstream.CircuitBreakerEnabled),
            upstreams.Count(static upstream => upstream.CircuitBreakerState == CircuitBreakerRuntimeState.Open),
            upstreams.Count(static upstream => upstream.CircuitBreakerState == CircuitBreakerRuntimeState.HalfOpen),
            upstreams.Count(static upstream => upstream.CircuitBreakerState == CircuitBreakerRuntimeState.Closed));
    }

    public static ProxyLimitSubsystemSummary BuildLimits(
        ProxyLimitConfigurationSummarySource? configuration,
        ProxyLimitRuntimeSummarySource runtime)
    {
        return new ProxyLimitSubsystemSummary(
            configuration?.MaxActiveClientConnections ?? 0,
            runtime.ActiveConnections,
            configuration?.MaxConcurrentTlsHandshakes ?? 0,
            runtime.ActiveTlsHandshakes,
            runtime.ActiveHttp2Streams,
            runtime.ActiveHttp3Streams,
            runtime.ActiveUpstreamHttp3Streams,
            configuration?.RequestsPerMinutePerIp ?? 0);
    }

    public static ProxyLogSubsystemSummary BuildLogs(ProxyLogSummarySource source)
    {
        return new ProxyLogSubsystemSummary(
            source.AccessLogPersistenceEnabled,
            source.AdminAuditPersistenceEnabled,
            source.State,
            source.Reason);
    }

    public static ProxyShutdownSubsystemSummary BuildShutdown(ProxyShutdownSummarySource source)
    {
        return new ProxyShutdownSubsystemSummary(
            source.IsRunning,
            source.IsShuttingDown,
            source.ShutdownStartedAtUtc,
            source.ShutdownDeadlineUtc);
    }

    public static ProxyProtocolSubsystemSummary BuildProtocols(
        IReadOnlyList<ProxyConfiguredListenerSummarySource> listeners,
        bool clientHttp3Enabled,
        bool clientHttp3Ready,
        IReadOnlyList<ProxyRouteSummarySource> routes)
    {
        return new ProxyProtocolSubsystemSummary(
            listeners.Any(static listener => listener.Enabled && listener.Http1Enabled),
            listeners.Any(static listener => listener.Enabled && listener.Http2Enabled),
            clientHttp3Enabled,
            clientHttp3Ready,
            routes.Any(static route => route.HasHttp3Upstream),
            RuntimeHttp3UnsupportedFeatureCodes.StatusSummary);
    }

}
