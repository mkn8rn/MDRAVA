using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;
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
    IReadOnlyList<ProxyUpstreamStatus> UpstreamHealth,
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
