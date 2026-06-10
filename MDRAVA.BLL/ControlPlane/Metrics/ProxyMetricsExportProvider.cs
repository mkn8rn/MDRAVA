using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed class ProxyMetricsExportProvider : IProxyMetricsExportProvider
{
    private readonly IProxyConfigurationStore _configurationStore;
    private readonly PrometheusMetricsExporter _exporter;
    private readonly ProxyMetricsExportAvailabilityService _availabilityService;

    public ProxyMetricsExportProvider(
        IProxyConfigurationStore configurationStore,
        PrometheusMetricsExporter exporter,
        ProxyMetricsExportAvailabilityService availabilityService)
    {
        _configurationStore = configurationStore;
        _exporter = exporter;
        _availabilityService = availabilityService;
    }

    public ProxyMetricsExportResult Export()
    {
        if (!_availabilityService.GetAvailability().Available)
        {
            return ProxyMetricsExportResult.NotAvailable;
        }

        if (!_configurationStore.TryGetSnapshot(out var snapshot) || snapshot is null)
        {
            return ProxyMetricsExportResult.NotAvailable;
        }

        return ProxyMetricsExportResult.Create(
            _exporter.Export(snapshot),
            PrometheusMetricsExporter.ContentType);
    }
}
