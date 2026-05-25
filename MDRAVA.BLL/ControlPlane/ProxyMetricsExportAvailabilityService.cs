namespace MDRAVA.BLL.ControlPlane;

public sealed class ProxyMetricsExportAvailabilityService
{
    private readonly IProxyMetricsExportAvailabilityReader _reader;

    public ProxyMetricsExportAvailabilityService(IProxyMetricsExportAvailabilityReader reader)
    {
        _reader = reader;
    }

    public ProxyMetricsExportAvailabilityResult GetAvailability()
    {
        var state = _reader.Read();
        return new ProxyMetricsExportAvailabilityResult(
            state.HasActiveConfiguration,
            state.MetricsExportEnabled,
            state.HasActiveConfiguration && state.MetricsExportEnabled);
    }
}
