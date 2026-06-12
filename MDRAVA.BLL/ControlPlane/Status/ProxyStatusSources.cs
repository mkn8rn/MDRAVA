using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.Listeners;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.BLL.ControlPlane.Status;

public interface IProxyStatusRuntimeStateSource
{
    ProxyRuntimeSnapshot ReadRuntime();
}

public interface IProxyStatusConfigurationSource
{
    bool TryReadSnapshot(out ProxyConfigurationSnapshot? snapshot);
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
