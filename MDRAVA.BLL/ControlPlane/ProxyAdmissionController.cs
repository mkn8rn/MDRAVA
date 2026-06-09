namespace MDRAVA.BLL.ControlPlane;

public sealed class ProxyAdmissionController
{
    private readonly IProxyAdmissionMetricsSink _metrics;
    private int _activeClientConnections;
    private int _activeTlsHandshakes;

    public ProxyAdmissionController(IProxyAdmissionMetricsSink metrics)
    {
        _metrics = metrics;
    }

    public AdmissionLease? TryAcquireClientConnection(int limit)
    {
        if (!TryIncrementBounded(ref _activeClientConnections, limit))
        {
            _metrics.ConnectionAdmissionRejected();
            return null;
        }

        return new AdmissionLease(() => Interlocked.Decrement(ref _activeClientConnections));
    }

    public AdmissionLease? TryAcquireTlsHandshake(int limit)
    {
        if (!TryIncrementBounded(ref _activeTlsHandshakes, limit))
        {
            _metrics.TlsHandshakeAdmissionRejected();
            return null;
        }

        _metrics.TlsHandshakeStarted();
        return new AdmissionLease(() =>
        {
            _metrics.TlsHandshakeEnded();
            Interlocked.Decrement(ref _activeTlsHandshakes);
        });
    }

    public int ActiveClientConnections => Volatile.Read(ref _activeClientConnections);

    public int ActiveTlsHandshakes => Volatile.Read(ref _activeTlsHandshakes);

    private static bool TryIncrementBounded(ref int field, int limit)
    {
        while (true)
        {
            var observed = Volatile.Read(ref field);
            if (observed >= limit)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref field, observed + 1, observed) == observed)
            {
                return true;
            }
        }
    }
}

public sealed class AdmissionLease : IDisposable
{
    private readonly Action _release;
    private int _disposed;

    public AdmissionLease(Action release)
    {
        _release = release;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _release();
        }
    }
}
