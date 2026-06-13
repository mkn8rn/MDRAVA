using MDRAVA.BLL.Configuration;
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

public sealed record ProxyStatusConfigurationSourceSet(
    ProxyStatusConfigurationSummary ConfigurationSummary,
    IReadOnlyList<ProxyUpstreamHealthSource> UpstreamHealthSources,
    Http3SupportConfigurationSource Http3Configuration,
    ProxyStatusReadinessConfigurationSourceSet ReadinessConfiguration);

public static class ProxyStatusConfigurationSourceMapper
{
    public static ProxyStatusConfigurationSourceSet FromConfiguration(
        ProxyConfigurationSnapshot configuration)
    {
        return new ProxyStatusConfigurationSourceSet(
            ProxyStatusConfigurationSummaryMapper.FromRuntimeConfiguration(
                configuration.Version,
                configuration.LoadedAtUtc,
                configuration.Listeners,
                configuration.Routes),
            ProxyUpstreamHealthSourceMapper.FromRoutes(configuration.Routes),
            Http3SupportSourceMapper.FromConfiguration(configuration.Listeners, configuration.Routes),
            ProxyStatusReadinessConfigurationSourceMapper.FromConfiguration(configuration));
    }
}

public interface IProxyStatusMetricsSource
{
    ProxyMetricsSnapshot ReadMetrics();
}

public interface IProxyStatusUpstreamHealthSource
{
    IReadOnlyList<ProxyUpstreamStatusResponse> ReadUpstreams(
        IReadOnlyList<ProxyUpstreamHealthSource> upstreams);
}

public interface IProxyStatusUpstreamHealthReader
{
    IReadOnlyList<ProxyUpstreamStatusResponse> ReadUpstreams();
}

public interface IProxyStatusRuntimePreflightSource
{
    ProxyRuntimePreflightStatus ReadRuntimePreflight();
}
