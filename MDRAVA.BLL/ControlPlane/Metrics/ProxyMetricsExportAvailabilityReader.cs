namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed class ProxyMetricsExportAvailabilityReader : IProxyMetricsExportAvailabilityReader
{
    private readonly IProxyMetricsExportConfigurationSource _configurationSource;

    public ProxyMetricsExportAvailabilityReader(
        IProxyMetricsExportConfigurationSource configurationSource)
    {
        _configurationSource = configurationSource;
    }

    public ProxyMetricsExportAvailabilityState Read()
    {
        var configuration = _configurationSource.ReadConfiguration();
        if (configuration is null)
        {
            return ProxyMetricsExportAvailabilityState.MissingConfiguration;
        }

        return ProxyMetricsExportAvailabilityState.FromConfiguration(configuration);
    }
}
