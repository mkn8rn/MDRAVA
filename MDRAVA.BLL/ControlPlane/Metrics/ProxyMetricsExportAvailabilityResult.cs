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

public sealed record ProxyMetricsExportAvailabilityResult
{
    private ProxyMetricsExportAvailabilityResult(
        bool hasActiveConfiguration,
        bool metricsExportEnabled)
    {
        HasActiveConfiguration = hasActiveConfiguration;
        MetricsExportEnabled = metricsExportEnabled;
    }

    public bool HasActiveConfiguration { get; }

    public bool MetricsExportEnabled { get; }

    public bool Available => HasActiveConfiguration && MetricsExportEnabled;

    public static ProxyMetricsExportAvailabilityResult FromState(
        ProxyMetricsExportAvailabilityState state)
    {
        return new ProxyMetricsExportAvailabilityResult(
            hasActiveConfiguration: state.HasActiveConfiguration,
            metricsExportEnabled: state.MetricsExportEnabled);
    }
}
