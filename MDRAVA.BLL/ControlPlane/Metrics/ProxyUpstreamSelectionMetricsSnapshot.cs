namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyUpstreamSelectionMetricsSnapshot(
    long Total,
    IReadOnlyList<ProxyUpstreamSelectionSnapshot> ByUpstream)
{
    public IReadOnlyList<ProxyUpstreamSelectionSnapshot> ByUpstream { get; } =
        MetricsList.Copy(ByUpstream);
}
