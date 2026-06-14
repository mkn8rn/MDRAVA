using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.INF.Observability;

public sealed class ProxyConfigurationMetricsExportConfigurationSource
    : IProxyMetricsExportConfigurationSource
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyConfigurationMetricsExportConfigurationSource(
        IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public ProxyMetricsExportConfigurationReadResult ReadConfiguration()
    {
        var snapshotResult = _configurationStore.ReadSnapshot();
        if (snapshotResult is not ProxyConfigurationSnapshotReadResult.AvailableResult available)
        {
            return ProxyMetricsExportConfigurationReadResult.MissingConfiguration;
        }

        var snapshot = available.Snapshot;
        return ProxyMetricsExportConfigurationReadResult.Available(
            ProxyMetricsExportConfigurationMapper.FromSources(
                snapshot.Metrics.Enabled,
                ProxyMetricsExportLabelOptionsMapper.FromMetrics(snapshot.Metrics),
                ProxyMetricsExportHttp3FactsMapper.FromRuntimeConfiguration(snapshot.Listeners, snapshot.Routes)));
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
