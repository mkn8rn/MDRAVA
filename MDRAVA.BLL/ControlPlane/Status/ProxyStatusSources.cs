using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.Listeners;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.BLL.ControlPlane.Status;

public interface IProxyStatusRuntimeStateSource
{
    ProxyStatusRuntimeSummary ReadRuntimeSummary();
}

public interface IProxyStatusConfigurationSource
{
    bool TryReadConfiguration(out ProxyStatusConfigurationSourceSet? configuration);
}

public sealed record ProxyStatusConfigurationSourceSet(
    int Version,
    DateTimeOffset LoadedAtUtc,
    IReadOnlyList<RuntimeListener> Listeners,
    IReadOnlyList<RuntimeRoute> Routes,
    ProxyStatusReadinessConfigurationSourceSet ReadinessConfiguration);

public static class ProxyStatusConfigurationSourceMapper
{
    public static ProxyStatusConfigurationSourceSet FromConfiguration(
        ProxyConfigurationSnapshot configuration)
    {
        return new ProxyStatusConfigurationSourceSet(
            configuration.Version,
            configuration.LoadedAtUtc,
            configuration.Listeners,
            configuration.Routes,
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
