using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using MDRAVA.BLL.ControlPlane.Status;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyMetricsExportInput(
    ProxyMetricsSnapshot Metrics,
    bool IncludePerRouteLabels,
    bool IncludePerUpstreamLabels,
    int DefaultEnabledHttp3ListenerCount,
    bool Http3RequestBodyStreamingEnabled,
    bool UpstreamHttp3MultiplexingConfigured,
    ProxyCacheStatus CacheStatus,
    IReadOnlyList<ProxyUpstreamStatusResponse> UpstreamHealth,
    IReadOnlyList<AcmeCertificateLifecycleStatus> AcmeCertificates);

public sealed record ProxyMetricsExportLabelOptions(
    bool IncludePerRouteLabels,
    bool IncludePerUpstreamLabels);

public sealed record ProxyMetricsExportHttp3Facts(
    int DefaultEnabledListenerCount,
    bool RequestBodyStreamingEnabled,
    bool UpstreamMultiplexingConfigured);

public sealed record ProxyMetricsExportConfiguration(
    bool MetricsEnabled,
    ProxyMetricsExportLabelOptions LabelOptions,
    ProxyMetricsExportHttp3Facts Http3Facts);

public static class ProxyMetricsExportConfigurationMapper
{
    public static ProxyMetricsExportConfiguration FromSources(
        bool metricsEnabled,
        ProxyMetricsExportLabelOptions labelOptions,
        ProxyMetricsExportHttp3Facts http3Facts)
    {
        return new ProxyMetricsExportConfiguration(
            metricsEnabled,
            labelOptions,
            http3Facts);
    }
}

public static class ProxyMetricsExportLabelOptionsMapper
{
    public static ProxyMetricsExportLabelOptions FromMetrics(RuntimeMetricsOptions metrics)
    {
        return new ProxyMetricsExportLabelOptions(
            metrics.IncludePerRouteLabels,
            metrics.IncludePerUpstreamLabels);
    }
}

public static class ProxyMetricsExportHttp3FactsMapper
{
    public static ProxyMetricsExportHttp3Facts FromRuntimeConfiguration(
        IReadOnlyList<RuntimeListener> listeners,
        IReadOnlyList<RuntimeRoute> routes)
    {
        return new ProxyMetricsExportHttp3Facts(
            listeners.Count(static listener =>
                listener.Http3.EnabledForTraffic
                && string.Equals(listener.Http3.EnablementLevel, "default", StringComparison.OrdinalIgnoreCase)),
            listeners.Any(static listener => listener.Http3.EnabledForTraffic),
            routes.Any(static route =>
                route.Upstreams.Any(static upstream => RuntimeUpstreamProtocol.IsHttp3(upstream.Protocol))));
    }
}

public interface IProxyMetricsExportInputSource
{
    ProxyMetricsExportInputReadResult ReadInput();
}

public interface IProxyMetricsExportConfigurationSource
{
    ProxyMetricsExportConfigurationReadResult ReadConfiguration();
}

public abstract record ProxyMetricsExportConfigurationReadResult
{
    private ProxyMetricsExportConfigurationReadResult()
    {
    }

    public static ProxyMetricsExportConfigurationReadResult MissingConfiguration { get; } =
        new MissingConfigurationResult();

    public static ProxyMetricsExportConfigurationReadResult Available(ProxyMetricsExportConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new AvailableResult(configuration);
    }

    public sealed record AvailableResult : ProxyMetricsExportConfigurationReadResult
    {
        public AvailableResult(ProxyMetricsExportConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            Configuration = configuration;
        }

        public ProxyMetricsExportConfiguration Configuration { get; }
    }

    public sealed record MissingConfigurationResult : ProxyMetricsExportConfigurationReadResult;
}

public abstract record ProxyMetricsExportInputReadResult
{
    private ProxyMetricsExportInputReadResult()
    {
    }

    public static ProxyMetricsExportInputReadResult MissingConfiguration { get; } =
        new MissingConfigurationResult();

    public static ProxyMetricsExportInputReadResult Available(ProxyMetricsExportInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return new AvailableResult(input);
    }

    public sealed record AvailableResult : ProxyMetricsExportInputReadResult
    {
        public AvailableResult(ProxyMetricsExportInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            Input = input;
        }

        public ProxyMetricsExportInput Input { get; }
    }

    public sealed record MissingConfigurationResult : ProxyMetricsExportInputReadResult;
}

public static class ProxyMetricsExportInputMapper
{
    public static ProxyMetricsExportInput FromSources(
        ProxyMetricsSnapshot metrics,
        ProxyMetricsExportLabelOptions labelOptions,
        ProxyMetricsExportHttp3Facts http3Facts,
        ProxyCacheStatus cacheStatus,
        IReadOnlyList<ProxyUpstreamStatusResponse> upstreamHealth,
        IReadOnlyList<AcmeCertificateLifecycleStatus> acmeCertificates)
    {
        return new ProxyMetricsExportInput(
            metrics,
            labelOptions.IncludePerRouteLabels,
            labelOptions.IncludePerUpstreamLabels,
            http3Facts.DefaultEnabledListenerCount,
            http3Facts.RequestBodyStreamingEnabled,
            http3Facts.UpstreamMultiplexingConfigured,
            cacheStatus,
            upstreamHealth,
            acmeCertificates);
    }
}

public sealed class ProxyConfigurationMetricsExportConfigurationSource
    : IProxyMetricsExportConfigurationSource
{
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyConfigurationMetricsExportConfigurationSource(
        IProxyConfigurationStore configurationStore)
    {
        _configurationStore = configurationStore;
    }

    public ProxyMetricsExportConfigurationReadResult ReadConfiguration()
    {
        var snapshotResult = _configurationStore.ReadSnapshot();
        if (snapshotResult is not ProxyConfigurationSnapshotReadResult.AvailableResult available)
        {
            return ProxyMetricsExportConfigurationReadResult.MissingConfiguration;
        }

        var snapshot = available.Snapshot;
        return ProxyMetricsExportConfigurationReadResult.Available(
            ProxyMetricsExportConfigurationMapper.FromSources(
                snapshot.Metrics.Enabled,
                ProxyMetricsExportLabelOptionsMapper.FromMetrics(snapshot.Metrics),
                ProxyMetricsExportHttp3FactsMapper.FromRuntimeConfiguration(snapshot.Listeners, snapshot.Routes)));
    }
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
