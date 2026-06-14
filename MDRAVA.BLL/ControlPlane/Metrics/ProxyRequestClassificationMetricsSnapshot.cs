namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyRequestClassificationMetricsSnapshot(
    IReadOnlyDictionary<string, long> FailuresByKind,
    IReadOnlyList<ProxyRequestSeriesSnapshot> ByRoute)
{
    public IReadOnlyDictionary<string, long> FailuresByKind { get; } =
        MetricsList.CopyDictionary(FailuresByKind, StringComparer.Ordinal);

    public IReadOnlyList<ProxyRequestSeriesSnapshot> ByRoute { get; } =
        MetricsList.Copy(ByRoute);
}
