namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyMetricsExportAvailabilityState(
    bool HasActiveConfiguration,
    bool MetricsExportEnabled)
{
    public static ProxyMetricsExportAvailabilityState MissingConfiguration { get; } = new(
        HasActiveConfiguration: false,
        MetricsExportEnabled: false);

    public static ProxyMetricsExportAvailabilityState FromConfiguration(
        ProxyMetricsExportConfiguration configuration)
    {
        return new ProxyMetricsExportAvailabilityState(
            HasActiveConfiguration: true,
            configuration.MetricsEnabled);
    }
}

public sealed record ProxyMetricsExportAvailabilityResult(
    bool HasActiveConfiguration,
    bool MetricsExportEnabled,
    bool Available);
