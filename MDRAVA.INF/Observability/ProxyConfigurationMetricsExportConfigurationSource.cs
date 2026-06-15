using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.INF.Observability;

public sealed class ProxyConfigurationMetricsExportConfigurationSource
    : IProxyMetricsExportConfigurationSource
{
    private readonly IProxyActiveConfigurationSnapshotReader _configurationStore;

    public ProxyConfigurationMetricsExportConfigurationSource(
        IProxyActiveConfigurationSnapshotReader configurationStore)
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
