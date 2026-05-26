using MDRAVA.API.Proxy.Metrics;

namespace MDRAVA.API.Proxy.Diagnostics;

public sealed class ProxyConfigLintMetricsSink : IProxyConfigLintMetricsSink
{
    private readonly ProxyMetrics _metrics;

    public ProxyConfigLintMetricsSink(ProxyMetrics metrics)
    {
        _metrics = metrics;
    }

    public void ConfigLintRun(IReadOnlyList<ConfigLintFinding> findings)
    {
        _metrics.ConfigLintRun(findings);
    }
}
