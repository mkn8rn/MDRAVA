using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.ConfigLint;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Observability;

namespace MDRAVA.BLL.ControlPlane.Status;

public sealed class ProxyStatusInputReader : IProxyStatusInputReader
{
    private readonly IProxyStatusRuntimeStateSource _runtimeSource;
    private readonly IProxyStatusMetricsSource _metricsSource;
    private readonly IProxyStatusConfigurationSource _configurationSource;
    private readonly IProxyStatusUpstreamHealthReader _upstreamReader;
    private readonly IProxyConfigLintOperations _lintOperations;
    private readonly IProxyLogPersistenceStore _logPersistenceStore;
    private readonly IProxyCacheStatusReader _cacheStatusReader;
    private readonly IProxyAcmeCertificateLifecycleStatusSource _acmeStatusSource;
    private readonly IProxyStatusRuntimePreflightSource _preflightSource;
    private readonly IRuntimeHttp3PlatformSupportSource _http3PlatformSupportSource;
    private readonly TimeProvider _timeProvider;

    public ProxyStatusInputReader(
        IProxyStatusRuntimeStateSource runtimeSource,
        IProxyStatusMetricsSource metricsSource,
        IProxyStatusConfigurationSource configurationSource,
        IProxyStatusUpstreamHealthReader upstreamReader,
        IProxyConfigLintOperations lintOperations,
        IProxyLogPersistenceStore logPersistenceStore,
        IProxyCacheStatusReader cacheStatusReader,
        IProxyAcmeCertificateLifecycleStatusSource acmeStatusSource,
        IProxyStatusRuntimePreflightSource preflightSource,
        IRuntimeHttp3PlatformSupportSource http3PlatformSupportSource,
        TimeProvider timeProvider)
    {
        _runtimeSource = runtimeSource;
        _metricsSource = metricsSource;
        _configurationSource = configurationSource;
        _upstreamReader = upstreamReader;
        _lintOperations = lintOperations;
        _logPersistenceStore = logPersistenceStore;
        _cacheStatusReader = cacheStatusReader;
        _acmeStatusSource = acmeStatusSource;
        _preflightSource = preflightSource;
        _http3PlatformSupportSource = http3PlatformSupportSource;
        _timeProvider = timeProvider;
    }

    public ProxyStatusInput Read()
    {
        var runtime = _runtimeSource.ReadRuntime();
        var configuration = _configurationSource.TryReadSnapshot(out var snapshot) ? snapshot : null;
        var upstreams = _upstreamReader.ReadUpstreams();
        var metrics = _metricsSource.ReadMetrics();
        var http3 = Http3RuntimeSupport.ProjectRuntime(
            Http3SupportSourceMapper.FromConfiguration(
                configuration?.Listeners ?? [],
                configuration?.Routes ?? []),
            _http3PlatformSupportSource.Read(),
            Http3SupportSourceMapper.FromRuntimeListeners(runtime.Listeners));
        var logPersistence = _logPersistenceStore.GetStatus();
        var runtimePreflight = _preflightSource.ReadRuntimePreflight();
        var cacheStatus = _cacheStatusReader.GetStatus();
        var acmeStatuses = _acmeStatusSource.GetLifecycleStatuses();
        var observedAtUtc = _timeProvider.GetUtcNow();
        var readiness = ProxyStatusReadinessInputMapper.FromSources(
            ProxyStatusReadinessSourceMapper.FromSources(
                configuration,
                runtime,
                metrics,
                upstreams,
                http3,
                logPersistence),
            cacheStatus,
            acmeStatuses,
            runtimePreflight,
            observedAtUtc);

        return new ProxyStatusInput(
            ProxyStatusRuntimeSummaryMapper.FromRuntime(runtime),
            ProxyStatusConfigurationSummaryMapper.FromSnapshot(configuration),
            metrics,
            upstreams,
            http3,
            logPersistence,
            cacheStatus,
            acmeStatuses,
            runtimePreflight,
            observedAtUtc,
            readiness,
            _lintOperations.LastActiveStatus);
    }
}
