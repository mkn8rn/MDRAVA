using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.Status;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyMetricsExportInput(
    ProxyMetricsSnapshot Metrics,
    bool IncludePerRouteLabels,
    bool IncludePerUpstreamLabels,
    int DefaultEnabledHttp3ListenerCount,
    bool Http3RequestBodyStreamingEnabled,
    bool UpstreamHttp3MultiplexingConfigured,
    ProxyCacheStatusResponse CacheStatus,
    IReadOnlyList<ProxyUpstreamStatusResponse> UpstreamHealth,
    IReadOnlyList<AcmeCertificateLifecycleStatus> AcmeCertificates);

public interface IProxyMetricsExportInputSource
{
    ProxyMetricsExportInput? ReadInput();
}

public static class ProxyMetricsExportInputMapper
{
    public static ProxyMetricsExportInput FromRuntime(
        ProxyConfigurationSnapshot snapshot,
        ProxyMetricsSnapshot metrics,
        ProxyCacheRuntimeStatusSnapshot cacheRuntime,
        IReadOnlyList<ProxyUpstreamStatusResponse> upstreamHealth,
        IReadOnlyList<AcmeCertificateLifecycleStatus> acmeCertificates)
    {
        var metricOptions = snapshot.Metrics;
        var cacheStatus = ProxyCacheStatusReader.Project(
            ProxyCacheStatusRouteSourceMapper.ToRouteSources(snapshot),
            cacheRuntime);

        return new ProxyMetricsExportInput(
            metrics,
            metricOptions.IncludePerRouteLabels,
            metricOptions.IncludePerUpstreamLabels,
            snapshot.Listeners.Count(static listener =>
                listener.Http3.EnabledForTraffic
                && string.Equals(listener.Http3.EnablementLevel, "default", StringComparison.OrdinalIgnoreCase)),
            snapshot.Listeners.Any(static listener => listener.Http3.EnabledForTraffic),
            snapshot.Routes.Any(static route =>
                route.Upstreams.Any(static upstream => RuntimeUpstreamProtocol.IsHttp3(upstream.Protocol))),
            cacheStatus,
            upstreamHealth,
            acmeCertificates);
    }
}

public sealed class ProxyMetricsExportInputSource : IProxyMetricsExportInputSource
{
    private readonly IProxyConfigurationStore _configurationStore;
    private readonly IProxyStatusMetricsSource _metricsSource;
    private readonly IProxyCacheRuntimeStatusSource _cacheRuntimeSource;
    private readonly IProxyStatusUpstreamHealthSource _upstreamHealthSource;
    private readonly IProxyAcmeCertificateLifecycleStatusSource _acmeStatusSource;

    public ProxyMetricsExportInputSource(
        IProxyConfigurationStore configurationStore,
        IProxyStatusMetricsSource metricsSource,
        IProxyCacheRuntimeStatusSource cacheRuntimeSource,
        IProxyStatusUpstreamHealthSource upstreamHealthSource,
        IProxyAcmeCertificateLifecycleStatusSource acmeStatusSource)
    {
        _configurationStore = configurationStore;
        _metricsSource = metricsSource;
        _cacheRuntimeSource = cacheRuntimeSource;
        _upstreamHealthSource = upstreamHealthSource;
        _acmeStatusSource = acmeStatusSource;
    }

    public ProxyMetricsExportInput? ReadInput()
    {
        if (!_configurationStore.TryGetSnapshot(out var snapshot) || snapshot is null)
        {
            return null;
        }

        return ProxyMetricsExportInputMapper.FromRuntime(
            snapshot,
            _metricsSource.ReadMetrics(),
            _cacheRuntimeSource.ReadSnapshot(),
            _upstreamHealthSource.ReadUpstreams(ProxyUpstreamHealthSourceMapper.FromSnapshot(snapshot)),
            _acmeStatusSource.GetLifecycleStatuses());
    }
}
