using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.BLL.ControlPlane.Status;

public interface IProxyStatusRuntimeStateSource
{
    ProxyStatusRuntimeSummary ReadRuntimeSummary();
}

public interface IProxyStatusConfigurationSource
{
    ProxyStatusConfigurationReadResult ReadConfiguration();
}

public abstract record ProxyStatusConfigurationReadResult
{
    private ProxyStatusConfigurationReadResult()
    {
    }

    public static ProxyStatusConfigurationReadResult MissingConfiguration { get; } =
        new MissingConfigurationResult();

    public static ProxyStatusConfigurationReadResult Available(ProxyStatusConfigurationSourceSet configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new AvailableResult(configuration);
    }

    public sealed record AvailableResult : ProxyStatusConfigurationReadResult
    {
        public AvailableResult(ProxyStatusConfigurationSourceSet configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            Configuration = configuration;
        }

        public ProxyStatusConfigurationSourceSet Configuration { get; }
    }

    public sealed record MissingConfigurationResult : ProxyStatusConfigurationReadResult;
}

public sealed record ProxyStatusConfigurationSourceSet
{
    public ProxyStatusConfigurationSourceSet(
        ProxyStatusConfigurationSummary ConfigurationSummary,
        IReadOnlyList<ProxyUpstreamHealthSource> UpstreamHealthSources,
        Http3SupportConfigurationSource Http3Configuration,
        ProxyStatusReadinessConfigurationSourceSet ReadinessConfiguration)
    {
        ArgumentNullException.ThrowIfNull(ConfigurationSummary);
        ArgumentNullException.ThrowIfNull(UpstreamHealthSources);
        ArgumentNullException.ThrowIfNull(Http3Configuration);
        ArgumentNullException.ThrowIfNull(ReadinessConfiguration);

        this.ConfigurationSummary = ConfigurationSummary;
        this.UpstreamHealthSources = ProxyStatusList.Copy(UpstreamHealthSources);
        this.Http3Configuration = Http3Configuration;
        this.ReadinessConfiguration = ReadinessConfiguration;
    }

    public ProxyStatusConfigurationSummary ConfigurationSummary { get; }

    public IReadOnlyList<ProxyUpstreamHealthSource> UpstreamHealthSources { get; }

    public Http3SupportConfigurationSource Http3Configuration { get; }

    public ProxyStatusReadinessConfigurationSourceSet ReadinessConfiguration { get; }
}

public interface IProxyStatusMetricsSource
{
    ProxyMetricsSnapshot ReadMetrics();
}

public interface IProxyStatusUpstreamHealthSource
{
    IReadOnlyList<ProxyUpstreamStatus> ReadUpstreams(
        IReadOnlyList<ProxyUpstreamHealthSource> upstreams);
}

public interface IProxyStatusUpstreamHealthReader
{
    IReadOnlyList<ProxyUpstreamStatus> ReadUpstreams();
}

public interface IProxyStatusRuntimePreflightSource
{
    ProxyRuntimePreflightStatus ReadRuntimePreflight();
}
