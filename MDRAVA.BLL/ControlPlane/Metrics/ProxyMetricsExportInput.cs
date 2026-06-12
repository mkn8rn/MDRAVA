using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
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

public sealed record ProxyMetricsExportConfiguration(
    bool MetricsEnabled,
    ProxyMetricsExportLabelOptions LabelOptions,
    ProxyMetricsExportHttp3Facts Http3Facts);

public static class ProxyMetricsExportLabelOptionsMapper
{
    public static ProxyMetricsExportLabelOptions FromMetrics(RuntimeMetricsOptions metrics)
    {
        return new ProxyMetricsExportLabelOptions(
            metrics.IncludePerRouteLabels,
            metrics.IncludePerUpstreamLabels);
    }
}

public static class ProxyMetricsExportHttp3FactsMapper
{
    public static ProxyMetricsExportHttp3Facts FromRuntimeConfiguration(
        IReadOnlyList<RuntimeListener> listeners,
        IReadOnlyList<RuntimeRoute> routes)
    {
        return new ProxyMetricsExportHttp3Facts(
            listeners.Count(static listener =>
                listener.Http3.EnabledForTraffic
                && string.Equals(listener.Http3.EnablementLevel, "default", StringComparison.OrdinalIgnoreCase)),
            listeners.Any(static listener => listener.Http3.EnabledForTraffic),
            routes.Any(static route =>
                route.Upstreams.Any(static upstream => RuntimeUpstreamProtocol.IsHttp3(upstream.Protocol))));
    }
}

public interface IProxyMetricsExportInputSource
{
    ProxyMetricsExportInput? ReadInput();
}

public interface IProxyMetricsExportConfigurationSource
{
    ProxyMetricsExportConfiguration? ReadConfiguration();
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

public sealed class ProxyConfigurationMetricsExportConfigurationSource
    : IProxyMetricsExportConfigurationSource
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyConfigurationMetricsExportConfigurationSource(
        IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public ProxyMetricsExportConfiguration? ReadConfiguration()
    {
        if (!_configurationStore.TryGetSnapshot(out var snapshot) || snapshot is null)
        {
            return null;
        }

        return new ProxyMetricsExportConfiguration(
            snapshot.Metrics.Enabled,
            ProxyMetricsExportLabelOptionsMapper.FromMetrics(snapshot.Metrics),
            ProxyMetricsExportHttp3FactsMapper.FromRuntimeConfiguration(snapshot.Listeners, snapshot.Routes));
    }
}

public sealed class ProxyMetricsExportInputSource : IProxyMetricsExportInputSource
{
    private readonly IProxyMetricsExportConfigurationSource _configurationSource;
    private readonly IProxyStatusMetricsSource _metricsSource;
    private readonly IProxyCacheStatusReader _cacheStatusReader;
    private readonly IProxyStatusUpstreamHealthReader _upstreamHealthReader;
    private readonly IProxyAcmeCertificateLifecycleStatusSource _acmeStatusSource;

    public ProxyMetricsExportInputSource(
        IProxyMetricsExportConfigurationSource configurationSource,
        IProxyStatusMetricsSource metricsSource,
        IProxyCacheStatusReader cacheStatusReader,
        IProxyStatusUpstreamHealthReader upstreamHealthReader,
        IProxyAcmeCertificateLifecycleStatusSource acmeStatusSource)
    {
        _configurationSource = configurationSource;
        _metricsSource = metricsSource;
        _cacheStatusReader = cacheStatusReader;
        _upstreamHealthReader = upstreamHealthReader;
        _acmeStatusSource = acmeStatusSource;
    }

    public ProxyMetricsExportInput? ReadInput()
    {
        var configuration = _configurationSource.ReadConfiguration();
        if (configuration is null)
        {
            return null;
        }

        return ProxyMetricsExportInputMapper.FromSources(
            _metricsSource.ReadMetrics(),
            configuration.LabelOptions,
            configuration.Http3Facts,
            _cacheStatusReader.GetStatus(),
            _upstreamHealthReader.ReadUpstreams(),
            _acmeStatusSource.GetLifecycleStatuses());
    }
}
