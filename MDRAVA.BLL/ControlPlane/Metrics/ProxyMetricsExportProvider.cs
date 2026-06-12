namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed class ProxyMetricsExportProvider : IProxyMetricsExportProvider
{
    private readonly IProxyMetricsExportInputSource _inputSource;
    private readonly PrometheusMetricsExporter _exporter;
    private readonly ProxyMetricsExportAvailabilityService _availabilityService;

    public ProxyMetricsExportProvider(
        IProxyMetricsExportInputSource inputSource,
        PrometheusMetricsExporter exporter,
        ProxyMetricsExportAvailabilityService availabilityService)
    {
        _inputSource = inputSource;
        _exporter = exporter;
        _availabilityService = availabilityService;
    }

    public ProxyMetricsExportResult Export()
    {
        if (!_availabilityService.GetAvailability().Available)
        {
            return ProxyMetricsExportResult.NotAvailable;
        }

        var input = _inputSource.ReadInput();
        if (input is null)
        {
            return ProxyMetricsExportResult.NotAvailable;
        }

        return ProxyMetricsExportResult.Create(
            _exporter.Export(input),
            PrometheusMetricsExporter.ContentType);
    }
}
