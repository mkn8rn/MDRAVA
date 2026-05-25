namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyMetricsExportAvailabilityState(
    bool HasActiveConfiguration,
    bool MetricsExportEnabled);

public sealed record ProxyMetricsExportAvailabilityResult(
    bool HasActiveConfiguration,
    bool MetricsExportEnabled,
    bool Available);
