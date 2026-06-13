namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class ProxyMetrics
{
    public void RequestReceived()
    {
        Interlocked.Increment(ref _totalRequests);
    }

    public void RequestCompleted(string? site, string? route, string? action, int? statusCode)
    {
        var key = new RequestSeriesKey(
            ProxyMetricLabelPolicy.NormalizeValue(site),
            ProxyMetricLabelPolicy.NormalizeValue(route),
            ProxyMetricLabelPolicy.NormalizeValue(action),
            ProxyMetricLabelPolicy.StatusClass(statusCode));
        var counter = _requestsByRoute.GetOrAdd(key, static _ => new RequestSeriesCounter());
        Interlocked.Increment(ref counter.Count);
    }
}
