using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane;

public sealed class CircuitBreakerLease : IDisposable
{
    private readonly Action<CircuitBreakerLease> _release;
    private int _completed;

    internal CircuitBreakerLease(
        RuntimeUpstream upstream,
        bool enabled,
        bool halfOpenProbe,
        Action<CircuitBreakerLease> release)
    {
        Upstream = upstream;
        Enabled = enabled;
        HalfOpenProbe = halfOpenProbe;
        _release = release;
    }

    public RuntimeUpstream Upstream { get; }

    public bool Enabled { get; }

    public bool HalfOpenProbe { get; }

    internal bool TryComplete()
    {
        return Interlocked.Exchange(ref _completed, 1) == 0;
    }

    public void Dispose()
    {
        if (TryComplete())
        {
            _release(this);
        }
    }
}
