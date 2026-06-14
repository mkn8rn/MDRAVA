using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.Status;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public interface IProxyMetricsExportInputSource
{
    ProxyMetricsExportInputReadResult ReadInput();
}

public interface IProxyMetricsExportConfigurationSource
{
    ProxyMetricsExportConfigurationReadResult ReadConfiguration();
}

public sealed class ProxyMetricsExportInputSource : IProxyMetricsExportInputSource
{
    private readonly IProxyMetricsExportConfigurationSource _configurationSource;
    private readonly IProxyStatusMetricsSource _metricsSource;
    private readonly IProxyCacheStatusReader _cacheStatusReader;
    private readonly IProxyStatusUpstreamHealthReader _upstreamHealthReader;
    private readonly IProxyAcmeCertificateLifecycleStatusSource _acmeStatusSource;

    public ProxyMetricsExportInputSource(
        IProxyMetricsExportConfigurationSource configurationSource,
        IProxyStatusMetricsSource metricsSource,
        IProxyCacheStatusReader cacheStatusReader,
        IProxyStatusUpstreamHealthReader upstreamHealthReader,
        IProxyAcmeCertificateLifecycleStatusSource acmeStatusSource)
    {
        _configurationSource = configurationSource;
        _metricsSource = metricsSource;
        _cacheStatusReader = cacheStatusReader;
        _upstreamHealthReader = upstreamHealthReader;
        _acmeStatusSource = acmeStatusSource;
    }

    public ProxyMetricsExportInputReadResult ReadInput()
    {
        var configurationResult = _configurationSource.ReadConfiguration();
        if (configurationResult is not ProxyMetricsExportConfigurationReadResult.AvailableResult available)
        {
            return ProxyMetricsExportInputReadResult.MissingConfiguration;
        }

        var configuration = available.Configuration;
        return ProxyMetricsExportInputReadResult.Available(ProxyMetricsExportInputMapper.FromSources(
            _metricsSource.ReadMetrics(),
            configuration.LabelOptions,
            configuration.Http3Facts,
            _cacheStatusReader.GetStatus(),
            _upstreamHealthReader.ReadUpstreams(),
            _acmeStatusSource.GetLifecycleStatuses()));
    }
}
