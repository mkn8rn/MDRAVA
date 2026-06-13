namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class ProxyMetrics
{
    private readonly record struct RequestSeriesKey(
        string Site,
        string Route,
        string Action,
        string StatusClass);

    private readonly record struct UpstreamSelectionKey(
        string Route,
        string Upstream,
        string Scheme,
        string Protocol);

    private readonly record struct Http3OutcomeKey(
        string Method,
        string Outcome,
        string StatusClass);

    private readonly record struct ConfigLintFindingKey(
        string Severity,
        string Code);

    private sealed class RequestSeriesCounter
    {
        public long Count;
    }
}
