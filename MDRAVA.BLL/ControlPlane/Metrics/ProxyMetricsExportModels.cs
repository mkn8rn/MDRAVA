using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.Status;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyMetricsExportInput
{
    public ProxyMetricsExportInput(
        ProxyMetricsSnapshot Metrics,
        bool IncludePerRouteLabels,
        bool IncludePerUpstreamLabels,
        int DefaultEnabledHttp3ListenerCount,
        bool Http3RequestBodyStreamingEnabled,
        bool UpstreamHttp3MultiplexingConfigured,
        ProxyCacheStatus CacheStatus,
        IReadOnlyList<ProxyUpstreamStatus> UpstreamHealth,
        IReadOnlyList<AcmeCertificateLifecycleStatus> AcmeCertificates)
    {
        ArgumentNullException.ThrowIfNull(Metrics);
        ArgumentNullException.ThrowIfNull(CacheStatus);
        ArgumentOutOfRangeException.ThrowIfNegative(
            DefaultEnabledHttp3ListenerCount,
            nameof(DefaultEnabledHttp3ListenerCount));

        this.Metrics = Metrics;
        this.IncludePerRouteLabels = IncludePerRouteLabels;
        this.IncludePerUpstreamLabels = IncludePerUpstreamLabels;
        this.DefaultEnabledHttp3ListenerCount = DefaultEnabledHttp3ListenerCount;
        this.Http3RequestBodyStreamingEnabled = Http3RequestBodyStreamingEnabled;
        this.UpstreamHttp3MultiplexingConfigured = UpstreamHttp3MultiplexingConfigured;
        this.CacheStatus = CacheStatus;
        this.UpstreamHealth = MetricsList.Copy(UpstreamHealth);
        this.AcmeCertificates = MetricsList.Copy(AcmeCertificates);
    }

    public ProxyMetricsSnapshot Metrics { get; }

    public bool IncludePerRouteLabels { get; }

    public bool IncludePerUpstreamLabels { get; }

    public int DefaultEnabledHttp3ListenerCount { get; }

    public bool Http3RequestBodyStreamingEnabled { get; }

    public bool UpstreamHttp3MultiplexingConfigured { get; }

    public ProxyCacheStatus CacheStatus { get; }

    public IReadOnlyList<ProxyUpstreamStatus> UpstreamHealth { get; }

    public IReadOnlyList<AcmeCertificateLifecycleStatus> AcmeCertificates { get; }
}

public sealed record ProxyMetricsExportLabelOptions(
    bool IncludePerRouteLabels,
    bool IncludePerUpstreamLabels);

public sealed record ProxyMetricsExportHttp3Facts
{
    public ProxyMetricsExportHttp3Facts(
        int DefaultEnabledListenerCount,
        bool RequestBodyStreamingEnabled,
        bool UpstreamMultiplexingConfigured)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(
            DefaultEnabledListenerCount,
            nameof(DefaultEnabledListenerCount));

        this.DefaultEnabledListenerCount = DefaultEnabledListenerCount;
        this.RequestBodyStreamingEnabled = RequestBodyStreamingEnabled;
        this.UpstreamMultiplexingConfigured = UpstreamMultiplexingConfigured;
    }

    public int DefaultEnabledListenerCount { get; }

    public bool RequestBodyStreamingEnabled { get; }

    public bool UpstreamMultiplexingConfigured { get; }
}

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
