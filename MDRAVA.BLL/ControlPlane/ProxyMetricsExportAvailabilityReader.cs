using MDRAVA.BLL.Infrastructure;

namespace MDRAVA.BLL.ControlPlane;

public sealed class ProxyMetricsExportAvailabilityReader : IProxyMetricsExportAvailabilityReader
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyMetricsExportAvailabilityReader(IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public ProxyMetricsExportAvailabilityState Read()
    {
        if (!_configurationStore.TryGetSnapshot(out var snapshot) || snapshot is null)
        {
            return new ProxyMetricsExportAvailabilityState(false, false);
        }

        return new ProxyMetricsExportAvailabilityState(true, snapshot.Metrics.Enabled);
    }
}
