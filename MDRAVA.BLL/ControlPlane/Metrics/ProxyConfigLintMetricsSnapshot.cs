using MDRAVA.BLL.ControlPlane.ConfigLint;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyConfigLintMetricsSnapshot(
    long Runs,
    IReadOnlyList<ProxyConfigLintFindingMetricSnapshot> Findings)
{
    public IReadOnlyList<ProxyConfigLintFindingMetricSnapshot> Findings { get; } =
        MetricsList.Copy(Findings);
}
