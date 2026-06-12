using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using MDRAVA.BLL.ControlPlane.Status;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed class ProxyMetricsExportProvider : IProxyMetricsExportProvider
{
    private readonly IProxyConfigurationStore _configurationStore;
    private readonly IProxyStatusMetricsSource _metricsSource;
    private readonly IProxyCacheRuntimeStatusSource _cacheRuntimeSource;
    private readonly IProxyStatusUpstreamHealthSource _upstreamHealthSource;
    private readonly IProxyAcmeCertificateLifecycleStatusSource _acmeStatusSource;
    private readonly PrometheusMetricsExporter _exporter;
    private readonly ProxyMetricsExportAvailabilityService _availabilityService;

    public ProxyMetricsExportProvider(
        IProxyConfigurationStore configurationStore,
        IProxyStatusMetricsSource metricsSource,
        IProxyCacheRuntimeStatusSource cacheRuntimeSource,
        IProxyStatusUpstreamHealthSource upstreamHealthSource,
        IProxyAcmeCertificateLifecycleStatusSource acmeStatusSource,
        PrometheusMetricsExporter exporter,
        ProxyMetricsExportAvailabilityService availabilityService)
    {
        _configurationStore = configurationStore;
        _metricsSource = metricsSource;
        _cacheRuntimeSource = cacheRuntimeSource;
        _upstreamHealthSource = upstreamHealthSource;
        _acmeStatusSource = acmeStatusSource;
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

        var input = ProxyMetricsExportInputMapper.FromRuntime(
            snapshot,
            _metricsSource.ReadMetrics(),
            _cacheRuntimeSource.ReadSnapshot(),
            _upstreamHealthSource.ReadUpstreams(snapshot),
            _acmeStatusSource.GetLifecycleStatuses());

        return ProxyMetricsExportResult.Create(
            _exporter.Export(input),
            PrometheusMetricsExporter.ContentType);
    }
}
