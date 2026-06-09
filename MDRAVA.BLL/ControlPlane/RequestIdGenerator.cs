using System.Globalization;

namespace MDRAVA.BLL.ControlPlane;

public sealed class RequestIdGenerator
{
    private readonly IProxyRequestIdMetricsSink _metrics;
    private long _nextId;

    public RequestIdGenerator(IProxyRequestIdMetricsSink metrics)
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
