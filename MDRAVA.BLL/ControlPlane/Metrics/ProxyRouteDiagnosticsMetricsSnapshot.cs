using MDRAVA.BLL.ControlPlane.RouteDiagnostics;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyRouteDiagnosticsMetricsSnapshot(
    long DryRuns,
    IReadOnlyList<ProxyRouteDryRunFailureSnapshot> DryRunFailures)
{
    public IReadOnlyList<ProxyRouteDryRunFailureSnapshot> DryRunFailures { get; } =
        MetricsList.Copy(DryRunFailures);
}
