using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.BLL.ControlPlane;

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
    IReadOnlyList<ProxyUpstreamStatusResponse> ReadUpstreams(ProxyConfigurationSnapshot? configuration);
}

public interface IProxyStatusRuntimePreflightSource
{
    ProxyRuntimePreflightStatus ReadRuntimePreflight();
}
