using MDRAVA.API.Proxy.Metrics;

namespace MDRAVA.API.Proxy.Diagnostics;

public sealed class ProxyRouteDiagnosticsMetricsSink
    : IProxyRouteDiagnosticsMetricsSink
{
    private readonly ProxyMetrics _metrics;

    public ProxyRouteDiagnosticsMetricsSink(ProxyMetrics metrics)
    {
        _metrics = metrics;
    }

    public void RouteMatchDryRun(string? failureReason)
    {
        _metrics.RouteMatchDryRun(failureReason);
    }
}
