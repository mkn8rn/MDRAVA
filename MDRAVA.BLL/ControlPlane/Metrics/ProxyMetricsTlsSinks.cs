namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class ProxyMetrics
{
    public void TlsHandshakeAttempted() => Interlocked.Increment(ref _tlsHandshakeAttempts);

    public void TlsHandshakeSucceeded() => Interlocked.Increment(ref _tlsHandshakeSuccesses);

    public void TlsHandshakeFailed() => Interlocked.Increment(ref _tlsHandshakeFailures);

    public void TlsHandshakeTimedOut() => Interlocked.Increment(ref _tlsHandshakeTimeouts);

    public void TlsNoCertificateForSni() => Interlocked.Increment(ref _tlsNoCertificateForSniFailures);
}
