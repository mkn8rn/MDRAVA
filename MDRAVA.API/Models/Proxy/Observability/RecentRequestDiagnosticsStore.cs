using MDRAVA.API.Proxy.Metrics;

namespace MDRAVA.API.Proxy.Observability;

public sealed class RecentRequestDiagnosticsStore
{
    public const int MaximumReadLimit = 500;

    private readonly ProxyMetrics _metrics;
    private readonly object _gate = new();
    private readonly LinkedList<ProxyRequestDiagnosticEvent> _events = new();

    public RecentRequestDiagnosticsStore(ProxyMetrics metrics)
    {
        _metrics = metrics;
    }

    public void Add(ProxyRequestDiagnosticEvent diagnostic, int capacity)
    {
        lock (_gate)
        {
            var boundedCapacity = Math.Max(1, capacity);
            _events.AddFirst(diagnostic);

            while (_events.Count > boundedCapacity)
            {
                _events.RemoveLast();
                _metrics.RecentDiagnosticOverwritten();
            }
        }
    }

    public IReadOnlyList<ProxyRequestDiagnosticEvent> Recent(int limit)
    {
        var boundedLimit = Math.Clamp(limit, 1, MaximumReadLimit);
        List<ProxyRequestDiagnosticEvent> results = [];

        lock (_gate)
        {
            var current = _events.First;
            while (current is not null && results.Count < boundedLimit)
            {
                results.Add(current.Value);
                current = current.Next;
            }
        }

        return results;
    }
}
