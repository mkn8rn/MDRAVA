using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.ConfigLint;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Listeners;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.BLL.ControlPlane.Status;

public sealed record ProxyStatusInput(
    ProxyStatusRuntimeSummary Runtime,
    ProxyStatusConfigurationSummary? Configuration,
    ProxyMetricsSnapshot Metrics,
    IReadOnlyList<ProxyUpstreamStatusResponse> Upstreams,
    RuntimeHttp3SupportProjection Http3,
    ProxyLogPersistenceStatus LogPersistence,
    ProxyCacheStatus? CacheStatus,
    IReadOnlyList<AcmeCertificateLifecycleStatus> AcmeStatuses,
    ProxyRuntimePreflightStatus RuntimePreflight,
    DateTimeOffset ObservedAtUtc,
    ProxyStatusReadinessInput Readiness,
    ConfigLintStatus ConfigLint);

public sealed record ProxyStatusRuntimeSummary(
    bool ListenerLive,
    string? ListenerName,
    string? Endpoint,
    DateTimeOffset? StartedAt,
    DateTimeOffset? StoppedAt,
    string? LastError,
    bool IsShuttingDown,
    DateTimeOffset? ShutdownStartedAtUtc,
    DateTimeOffset? ShutdownDeadlineUtc,
    IReadOnlyList<ProxyListenerStatus> Listeners,
    ProxyListenerReloadResult? LastListenerReload);

public static class ProxyStatusRuntimeSummaryMapper
{
    public static ProxyStatusRuntimeSummary FromRuntime(ProxyRuntimeSnapshot runtime)
    {
        return new ProxyStatusRuntimeSummary(
            runtime.IsRunning,
            runtime.ListenerName,
            runtime.Endpoint,
            runtime.StartedAt,
            runtime.StoppedAt,
            runtime.LastError,
            runtime.IsShuttingDown,
            runtime.ShutdownStartedAtUtc,
            runtime.ShutdownDeadlineUtc,
            runtime.Listeners,
            runtime.LastListenerReload);
    }
}

public sealed record ProxyStatusConfigurationSummary(
    int Version,
    DateTimeOffset LoadedAtUtc,
    int ListenerCount,
    int RouteCount);

public static class ProxyStatusConfigurationSummaryMapper
{
    public static ProxyStatusConfigurationSummary FromRuntimeConfiguration(
        int version,
        DateTimeOffset loadedAtUtc,
        IReadOnlyList<RuntimeListener> listeners,
        IReadOnlyList<RuntimeRoute> routes)
    {
        return new ProxyStatusConfigurationSummary(
            version,
            loadedAtUtc,
            listeners.Count,
            routes.Count);
    }
}

public interface IProxyStatusInputReader
{
    ProxyStatusInput Read();
}
