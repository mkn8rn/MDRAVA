namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class ProxyMetrics
{
    public void ConnectionAdmissionRejected() => Interlocked.Increment(ref _connectionAdmissionRejections);

    public void TlsHandshakeStarted() => Interlocked.Increment(ref _activeTlsHandshakes);

    public void TlsHandshakeEnded() => Interlocked.Decrement(ref _activeTlsHandshakes);

    public void TlsHandshakeAdmissionRejected() => Interlocked.Increment(ref _tlsHandshakeAdmissionRejections);

    public void RequestRateLimited() => Interlocked.Increment(ref _rateLimitedRequests);

    public void UpgradeRateLimited() => Interlocked.Increment(ref _rateLimitedUpgrades);

    public void RequestBodySizeRejected() => Interlocked.Increment(ref _requestBodySizeRejections);

    public void ParserLimitRejected() => Interlocked.Increment(ref _parserLimitRejections);
}
