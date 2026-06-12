namespace MDRAVA.BLL.ControlPlane.RuntimeGuards;

public sealed class ProxyAdmissionController
{
    private readonly IProxyAdmissionMetricsSink _metrics;
    private int _activeClientConnections;
    private int _activeTlsHandshakes;

    public ProxyAdmissionController(IProxyAdmissionMetricsSink metrics)
    {
        _metrics = metrics;
    }

    public ProxyAdmissionDecision AcquireClientConnection(int limit)
    {
        if (!TryIncrementBounded(ref _activeClientConnections, limit))
        {
            _metrics.ConnectionAdmissionRejected();
            return ProxyAdmissionDecision.Rejected;
        }

        return ProxyAdmissionDecision.Accepted(
            new AdmissionLease(() => Interlocked.Decrement(ref _activeClientConnections)));
    }

    public ProxyAdmissionDecision AcquireTlsHandshake(int limit)
    {
        if (!TryIncrementBounded(ref _activeTlsHandshakes, limit))
        {
            _metrics.TlsHandshakeAdmissionRejected();
            return ProxyAdmissionDecision.Rejected;
        }

        _metrics.TlsHandshakeStarted();
        return ProxyAdmissionDecision.Accepted(
            new AdmissionLease(() =>
            {
                _metrics.TlsHandshakeEnded();
                Interlocked.Decrement(ref _activeTlsHandshakes);
            }));
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

public abstract record ProxyAdmissionDecision
{
    private ProxyAdmissionDecision()
    {
    }

    public static ProxyAdmissionDecision Rejected { get; } = new RejectedResult();

    public static ProxyAdmissionDecision Accepted(AdmissionLease lease)
    {
        ArgumentNullException.ThrowIfNull(lease);
        return new AcceptedResult(lease);
    }

    public sealed record AcceptedResult : ProxyAdmissionDecision
    {
        public AcceptedResult(AdmissionLease lease)
        {
            ArgumentNullException.ThrowIfNull(lease);
            Lease = lease;
        }

        public AdmissionLease Lease { get; }
    }

    public sealed record RejectedResult : ProxyAdmissionDecision;
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
