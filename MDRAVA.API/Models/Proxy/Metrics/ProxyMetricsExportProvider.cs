using MDRAVA.API.Proxy.Configuration.Storage;

namespace MDRAVA.API.Proxy.Metrics;

public sealed class ProxyMetricsExportProvider : IProxyMetricsExportProvider
{
    private readonly IProxyConfigurationStore _configurationStore;
    private readonly PrometheusMetricsExporter _exporter;

    public ProxyMetricsExportProvider(
        IProxyConfigurationStore configurationStore,
        PrometheusMetricsExporter exporter)
    {
        _configurationStore = configurationStore;
        _exporter = exporter;
    }

    public ProxyMetricsExportResult Export()
    {
        if (!_configurationStore.TryGetSnapshot(out var snapshot) || snapshot is null)
        {
            return ProxyMetricsExportResult.NotAvailable;
        }

        if (!snapshot.Metrics.Enabled)
        {
            return ProxyMetricsExportResult.NotAvailable;
        }

        return ProxyMetricsExportResult.Create(
            _exporter.Export(snapshot),
            PrometheusMetricsExporter.ContentType);
    }
}
