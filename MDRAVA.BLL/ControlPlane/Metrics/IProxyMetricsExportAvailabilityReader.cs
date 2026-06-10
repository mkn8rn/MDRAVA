namespace MDRAVA.BLL.ControlPlane.Metrics;

public interface IProxyMetricsExportAvailabilityReader
{
    ProxyMetricsExportAvailabilityState Read();
}
