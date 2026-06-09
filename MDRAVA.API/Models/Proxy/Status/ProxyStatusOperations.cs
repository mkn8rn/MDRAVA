using MDRAVA.API.Proxy.Acme;
using MDRAVA.API.Proxy.Caching;
using MDRAVA.API.Proxy.Configuration.Storage;
using MDRAVA.API.Proxy.Diagnostics;
using MDRAVA.API.Proxy.Health;
using MDRAVA.API.Proxy.Hosting;
using MDRAVA.API.Proxy.Metrics;
using MDRAVA.API.Proxy.Runtime;

namespace MDRAVA.API.Proxy.Status;

public sealed class ProxyStatusOperations : IProxyStatusOperations
{
    private readonly ProxyRuntimeState _runtimeState;
    private readonly ProxyMetrics _metrics;
    private readonly IProxyConfigurationStore _configurationStore;
    private readonly UpstreamHealthStore _healthStore;
    private readonly IProxyConfigLintOperations? _lintService;
    private readonly IProxyLogPersistenceStore? _logPersistenceStore;
    private readonly ResponseCacheStore? _cacheStore;
    private readonly AcmeCertificateStatusStore? _acmeStatusStore;
    private readonly ProxyRuntimePreflightService? _preflightService;

    public ProxyStatusOperations(
        ProxyRuntimeState runtimeState,
        ProxyMetrics metrics,
        IProxyConfigurationStore configurationStore,
        UpstreamHealthStore healthStore,
        IProxyConfigLintOperations? lintService = null,
        IProxyLogPersistenceStore? logPersistenceStore = null,
        ResponseCacheStore? cacheStore = null,
        AcmeCertificateStatusStore? acmeStatusStore = null,
        ProxyRuntimePreflightService? preflightService = null)
    {
        _runtimeState = runtimeState;
        _metrics = metrics;
        _configurationStore = configurationStore;
        _healthStore = healthStore;
        _lintService = lintService;
        _logPersistenceStore = logPersistenceStore;
        _cacheStore = cacheStore;
        _acmeStatusStore = acmeStatusStore;
        _preflightService = preflightService;
    }

    public ProxyStatusResponse GetStatus()
    {
        var runtime = _runtimeState.Snapshot();
        var listenerCount = _configurationStore.TryGetSnapshot(out var snapshot) && snapshot is not null
            ? snapshot.Listeners.Count
            : 0;
        var routeCount = snapshot?.Routes.Count ?? 0;

        var upstreams = _healthStore.Snapshot(snapshot)
            .Select(static upstream => new ProxyUpstreamStatusResponse(
                upstream.RouteName,
                upstream.UpstreamName,
                upstream.Endpoint,
                upstream.Scheme,
                upstream.TlsCertificateValidationEnabled,
                upstream.SniHost,
                upstream.HealthCheckEnabled,
                upstream.State,
                upstream.LastResult,
                upstream.LastCheckedAtUtc,
                upstream.ConsecutiveSuccesses,
                upstream.ConsecutiveFailures,
                upstream.SelectedRequests,
                upstream.RequestFailures)
            {
                Protocol = upstream.Protocol,
                Weight = upstream.Weight,
                CircuitBreaker = upstream.CircuitBreaker
            })
            .ToArray();
        var metrics = _metrics.Snapshot();
        var http3 = Http3RuntimeSupport.Project(snapshot?.Listeners ?? [], runtime.Listeners, snapshot?.Routes);
        var logPersistence = _logPersistenceStore?.GetStatus() ?? ProxyLogPersistenceStatus.Unknown;
        var runtimePreflight = _preflightService?.LastStatus ?? ProxyRuntimePreflightStatus.Unknown;
        var cacheStatus = _cacheStore?.Snapshot(snapshot);
        var acmeStatuses = _acmeStatusStore?.Snapshot() ?? [];
        var (readiness, subsystems) = ProxyStatusReadinessBuilder.Build(
            snapshot,
            runtime,
            metrics,
            upstreams,
            http3,
            logPersistence,
            cacheStatus,
            acmeStatuses,
            runtimePreflight);

        return new ProxyStatusResponse(
            runtime.IsRunning,
            runtime.ListenerName,
            runtime.Endpoint,
            runtime.StartedAt,
            runtime.StoppedAt,
            runtime.LastError,
            runtime.IsShuttingDown,
            runtime.ShutdownStartedAtUtc,
            runtime.ShutdownDeadlineUtc,
            snapshot?.Version,
            snapshot?.LoadedAtUtc,
            listenerCount,
            routeCount,
            metrics,
            upstreams)
        {
            Listeners = runtime.Listeners,
            LastListenerReload = runtime.LastListenerReload,
            Http3 = http3,
            RouteDiagnostics = RouteDiagnosticsStatus.Enabled,
            ConfigLint = _lintService?.LastActiveStatus ?? ConfigLintStatus.Empty,
            LogPersistence = logPersistence,
            Readiness = readiness,
            Subsystems = subsystems,
            RuntimePreflight = runtimePreflight
        };
    }
}
