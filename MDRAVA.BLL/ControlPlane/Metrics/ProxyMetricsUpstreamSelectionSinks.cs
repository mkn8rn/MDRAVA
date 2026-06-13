using MDRAVA.BLL.ControlPlane.UpstreamSelection;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class ProxyMetrics
{
    public void UpstreamSelected(ProxyUpstreamSelectionMetric selection)
    {
        Interlocked.Increment(ref _upstreamSelections);
        var key = new UpstreamSelectionKey(
            ProxyMetricLabelPolicy.NormalizeValue(selection.Route),
            ProxyMetricLabelPolicy.NormalizeValue(selection.Upstream),
            ProxyMetricLabelPolicy.NormalizeValue(selection.Scheme),
            ProxyMetricLabelPolicy.NormalizeValue(selection.Protocol));
        var counter = _upstreamSelectionsByUpstream.GetOrAdd(key, static _ => new RequestSeriesCounter());
        Interlocked.Increment(ref counter.Count);
    }

    public void NoHealthyUpstream() => Interlocked.Increment(ref _noHealthyUpstreamFailures);

    public void NoAvailableUpstream() => Interlocked.Increment(ref _noAvailableUpstreamFailures);
}
