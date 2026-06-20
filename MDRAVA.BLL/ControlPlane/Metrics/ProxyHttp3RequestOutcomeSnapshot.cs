namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyHttp3RequestOutcomeSnapshot(
    string Method,
    string Outcome,
    string StatusClass,
    long Count)
{
    public long Count { get; } = MetricsList.RequireCounter(Count, nameof(Count));
}
