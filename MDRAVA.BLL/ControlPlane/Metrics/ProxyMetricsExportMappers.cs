using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.Status;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public static class ProxyMetricsExportConfigurationMapper
{
    public static ProxyMetricsExportConfiguration FromSources(
        bool metricsEnabled,
        ProxyMetricsExportLabelOptions labelOptions,
        ProxyMetricsExportHttp3Facts http3Facts)
    {
        return new ProxyMetricsExportConfiguration(
            metricsEnabled,
            labelOptions,
            http3Facts);
    }
}

public static class ProxyMetricsExportInputMapper
{
    public static ProxyMetricsExportInput FromSources(
        ProxyMetricsSnapshot metrics,
        ProxyMetricsExportLabelOptions labelOptions,
        ProxyMetricsExportHttp3Facts http3Facts,
        ProxyCacheStatus cacheStatus,
        IReadOnlyList<ProxyUpstreamStatus> upstreamHealth,
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
