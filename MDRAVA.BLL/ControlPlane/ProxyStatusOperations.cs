using MDRAVA.BLL.Infrastructure;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane;

public sealed class ProxyStatusOperations : IProxyStatusOperations
{
    private readonly IProxyStatusRuntimeStateSource _runtimeSource;
    private readonly IProxyStatusMetricsSource _metricsSource;
    private readonly IProxyStatusConfigurationSource _configurationSource;
    private readonly IProxyStatusUpstreamHealthSource _upstreamSource;
    private readonly IProxyConfigLintOperations _lintOperations;
    private readonly IProxyLogPersistenceStore _logPersistenceStore;
    private readonly IProxyCacheStatusReader _cacheStatusReader;
    private readonly IProxyAcmeCertificateLifecycleStatusSource _acmeStatusSource;
    private readonly IProxyStatusRuntimePreflightSource _preflightSource;

    public ProxyStatusOperations(
        IProxyStatusRuntimeStateSource runtimeSource,
        IProxyStatusMetricsSource metricsSource,
        IProxyStatusConfigurationSource configurationSource,
        IProxyStatusUpstreamHealthSource upstreamSource,
        IProxyConfigLintOperations lintOperations,
        IProxyLogPersistenceStore logPersistenceStore,
        IProxyCacheStatusReader cacheStatusReader,
        IProxyAcmeCertificateLifecycleStatusSource acmeStatusSource,
        IProxyStatusRuntimePreflightSource preflightSource)
    {
        _runtimeSource = runtimeSource;
        _metricsSource = metricsSource;
        _configurationSource = configurationSource;
        _upstreamSource = upstreamSource;
        _lintOperations = lintOperations;
        _logPersistenceStore = logPersistenceStore;
        _cacheStatusReader = cacheStatusReader;
        _acmeStatusSource = acmeStatusSource;
        _preflightSource = preflightSource;
    }

    public ProxyStatusResponse GetStatus()
    {
        var runtime = _runtimeSource.ReadRuntime();
        var listenerCount = _configurationSource.TryReadSnapshot(out var snapshot) && snapshot is not null
            ? snapshot.Listeners.Count
            : 0;
        var routeCount = snapshot?.Routes.Count ?? 0;

        var upstreams = _upstreamSource.ReadUpstreams(snapshot);
        var metrics = _metricsSource.ReadMetrics();
        var http3 = Http3RuntimeSupport.Project(snapshot?.Listeners ?? [], runtime.Listeners, snapshot?.Routes);
        var logPersistence = _logPersistenceStore.GetStatus();
        var runtimePreflight = _preflightSource.ReadRuntimePreflight();
        var cacheStatus = _cacheStatusReader.GetStatus();
        var acmeStatuses = _acmeStatusSource.GetLifecycleStatuses();
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
            ConfigLint = _lintOperations.LastActiveStatus,
            LogPersistence = logPersistence,
            Readiness = readiness,
            Subsystems = subsystems,
            RuntimePreflight = runtimePreflight
        };
    }
}
