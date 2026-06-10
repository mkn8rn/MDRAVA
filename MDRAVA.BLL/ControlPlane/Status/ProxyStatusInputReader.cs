using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.ConfigLint;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Observability;
using MDRAVA.BLL.Infrastructure;

namespace MDRAVA.BLL.ControlPlane.Status;

public sealed class ProxyStatusInputReader : IProxyStatusInputReader
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
    private readonly IRuntimeHttp3PlatformSupportSource _http3PlatformSupportSource;

    public ProxyStatusInputReader(
        IProxyStatusRuntimeStateSource runtimeSource,
        IProxyStatusMetricsSource metricsSource,
        IProxyStatusConfigurationSource configurationSource,
        IProxyStatusUpstreamHealthSource upstreamSource,
        IProxyConfigLintOperations lintOperations,
        IProxyLogPersistenceStore logPersistenceStore,
        IProxyCacheStatusReader cacheStatusReader,
        IProxyAcmeCertificateLifecycleStatusSource acmeStatusSource,
        IProxyStatusRuntimePreflightSource preflightSource,
        IRuntimeHttp3PlatformSupportSource http3PlatformSupportSource)
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
        _http3PlatformSupportSource = http3PlatformSupportSource;
    }

    public ProxyStatusInput Read()
    {
        var runtime = _runtimeSource.ReadRuntime();
        var configuration = _configurationSource.TryReadSnapshot(out var snapshot) ? snapshot : null;
        var upstreams = _upstreamSource.ReadUpstreams(configuration);
        var metrics = _metricsSource.ReadMetrics();
        var http3 = Http3RuntimeSupport.Project(
            configuration?.Listeners ?? [],
            _http3PlatformSupportSource.Read(),
            runtime.Listeners,
            configuration?.Routes);
        var logPersistence = _logPersistenceStore.GetStatus();
        var runtimePreflight = _preflightSource.ReadRuntimePreflight();
        var cacheStatus = _cacheStatusReader.GetStatus();
        var acmeStatuses = _acmeStatusSource.GetLifecycleStatuses();

        return new ProxyStatusInput(
            runtime,
            configuration,
            metrics,
            upstreams,
            http3,
            logPersistence,
            cacheStatus,
            acmeStatuses,
            runtimePreflight,
            _lintOperations.LastActiveStatus);
    }
}
