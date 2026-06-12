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

public sealed record ProxyMetricsExportLabelOptions(
    bool IncludePerRouteLabels,
    bool IncludePerUpstreamLabels);

public sealed record ProxyMetricsExportHttp3Facts(
    int DefaultEnabledListenerCount,
    bool RequestBodyStreamingEnabled,
    bool UpstreamMultiplexingConfigured);

public interface IProxyMetricsExportInputSource
{
    ProxyMetricsExportInput? ReadInput();
}

public static class ProxyMetricsExportInputMapper
{
    public static ProxyMetricsExportInput FromSources(
        ProxyMetricsSnapshot metrics,
        ProxyMetricsExportLabelOptions labelOptions,
        ProxyMetricsExportHttp3Facts http3Facts,
        ProxyCacheStatusResponse cacheStatus,
        IReadOnlyList<ProxyUpstreamStatusResponse> upstreamHealth,
        IReadOnlyList<AcmeCertificateLifecycleStatus> acmeCertificates)
    {
        return new ProxyMetricsExportInput(
            metrics,
            labelOptions.IncludePerRouteLabels,
            labelOptions.IncludePerUpstreamLabels,
            http3Facts.DefaultEnabledListenerCount,
            http3Facts.RequestBodyStreamingEnabled,
            http3Facts.UpstreamMultiplexingConfigured,
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

        var cacheStatus = ProxyCacheStatusReader.Project(
            ProxyCacheStatusRouteSourceMapper.ToRouteSources(snapshot),
            _cacheRuntimeSource.ReadSnapshot());

        return ProxyMetricsExportInputMapper.FromSources(
            _metricsSource.ReadMetrics(),
            new ProxyMetricsExportLabelOptions(
                snapshot.Metrics.IncludePerRouteLabels,
                snapshot.Metrics.IncludePerUpstreamLabels),
            new ProxyMetricsExportHttp3Facts(
                snapshot.Listeners.Count(static listener =>
                    listener.Http3.EnabledForTraffic
                    && string.Equals(listener.Http3.EnablementLevel, "default", StringComparison.OrdinalIgnoreCase)),
                snapshot.Listeners.Any(static listener => listener.Http3.EnabledForTraffic),
                snapshot.Routes.Any(static route =>
                    route.Upstreams.Any(static upstream => RuntimeUpstreamProtocol.IsHttp3(upstream.Protocol)))),
            cacheStatus,
            _upstreamHealthSource.ReadUpstreams(ProxyUpstreamHealthSourceMapper.FromSnapshot(snapshot)),
            _acmeStatusSource.GetLifecycleStatuses());
    }
}
