using MDRAVA.BLL.ControlPlane.RouteDiagnostics;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyRouteDiagnosticsMetricsSnapshot
{
    public ProxyRouteDiagnosticsMetricsSnapshot(
        long DryRuns,
        IEnumerable<ProxyRouteDryRunFailureSnapshot> DryRunFailures)
    {
        this.DryRuns = DryRuns;
        this.DryRunFailures = MetricsList.Copy(DryRunFailures);
    }

    public long DryRuns { get; }

    public IReadOnlyList<ProxyRouteDryRunFailureSnapshot> DryRunFailures { get; }
}
