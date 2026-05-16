using System.Globalization;
using MDRAVA.API.Proxy.Metrics;

namespace MDRAVA.API.Proxy.Observability;

public sealed class RequestIdGenerator
{
    private readonly ProxyMetrics _metrics;
    private long _nextId;

    public RequestIdGenerator(ProxyMetrics metrics)
    {
        _metrics = metrics;
    }

    public string Create()
    {
        var value = Interlocked.Increment(ref _nextId);
        _metrics.RequestIdGenerated();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"mdr-{Environment.ProcessId:x}-{value:x}");
    }
}
