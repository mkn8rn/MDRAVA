namespace MDRAVA.API.Proxy.Metrics;

public sealed class ProxyMetrics
{
    private long _acceptedConnections;
    private long _activeConnections;
    private long _totalRequests;
    private long _upstreamSuccesses;
    private long _upstreamFailures;
    private long _bytesRead;
    private long _bytesWritten;
    private long _parseErrors;
    private long _rejectedMalformedRequests;
    private long _rejectedUnsupportedRequestFraming;
    private long _upstreamMalformedResponses;
    private long _clientBodyRelayFailures;
    private long _upstreamBodyRelayFailures;
    private long _clientRequestHeadTimeouts;
    private long _clientRequestBodyTimeouts;
    private long _upstreamConnectFailures;
    private long _upstreamConnectTimeouts;
    private long _upstreamResponseHeadTimeouts;
    private long _upstreamResponseBodyTimeouts;
    private long _upstreamPrematureDisconnects;
    private long _clientPrematureDisconnects;
    private long _proxyGenerated502Responses;
    private long _proxyGenerated504Responses;
    private long _downstreamWriteTimeouts;
    private long _tlsHandshakeAttempts;
    private long _tlsHandshakeSuccesses;
    private long _tlsHandshakeFailures;
    private long _tlsHandshakeTimeouts;
    private long _tlsNoCertificateForSniFailures;

    public void ConnectionAccepted()
    {
        Interlocked.Increment(ref _acceptedConnections);
        Interlocked.Increment(ref _activeConnections);
    }

    public void ConnectionClosed()
    {
        Interlocked.Decrement(ref _activeConnections);
    }

    public void RequestReceived()
    {
        Interlocked.Increment(ref _totalRequests);
    }

    public void UpstreamSucceeded()
    {
        Interlocked.Increment(ref _upstreamSuccesses);
    }

    public void UpstreamFailed()
    {
        Interlocked.Increment(ref _upstreamFailures);
    }

    public void UpstreamConnectFailed()
    {
        Interlocked.Increment(ref _upstreamConnectFailures);
    }

    public void AddBytesRead(long bytes)
    {
        if (bytes > 0)
        {
            Interlocked.Add(ref _bytesRead, bytes);
        }
    }

    public void AddBytesWritten(long bytes)
    {
        if (bytes > 0)
        {
            Interlocked.Add(ref _bytesWritten, bytes);
        }
    }

    public void ParseFailed()
    {
        Interlocked.Increment(ref _parseErrors);
    }

    public void MalformedRequestRejected()
    {
        Interlocked.Increment(ref _rejectedMalformedRequests);
    }

    public void UnsupportedRequestFramingRejected()
    {
        Interlocked.Increment(ref _rejectedUnsupportedRequestFraming);
    }

    public void UpstreamMalformedResponse()
    {
        Interlocked.Increment(ref _upstreamMalformedResponses);
    }

    public void ClientBodyRelayFailed()
    {
        Interlocked.Increment(ref _clientBodyRelayFailures);
    }

    public void UpstreamBodyRelayFailed()
    {
        Interlocked.Increment(ref _upstreamBodyRelayFailures);
    }

    public void ClientRequestHeadTimedOut() => Interlocked.Increment(ref _clientRequestHeadTimeouts);

    public void ClientRequestBodyTimedOut() => Interlocked.Increment(ref _clientRequestBodyTimeouts);

    public void UpstreamConnectTimedOut() => Interlocked.Increment(ref _upstreamConnectTimeouts);

    public void UpstreamResponseHeadTimedOut() => Interlocked.Increment(ref _upstreamResponseHeadTimeouts);

    public void UpstreamResponseBodyTimedOut() => Interlocked.Increment(ref _upstreamResponseBodyTimeouts);

    public void UpstreamPrematureDisconnect() => Interlocked.Increment(ref _upstreamPrematureDisconnects);

    public void ClientPrematureDisconnect() => Interlocked.Increment(ref _clientPrematureDisconnects);

    public void ProxyGenerated502() => Interlocked.Increment(ref _proxyGenerated502Responses);

    public void ProxyGenerated504() => Interlocked.Increment(ref _proxyGenerated504Responses);

    public void DownstreamWriteTimedOut() => Interlocked.Increment(ref _downstreamWriteTimeouts);

    public void TlsHandshakeAttempted() => Interlocked.Increment(ref _tlsHandshakeAttempts);

    public void TlsHandshakeSucceeded() => Interlocked.Increment(ref _tlsHandshakeSuccesses);

    public void TlsHandshakeFailed() => Interlocked.Increment(ref _tlsHandshakeFailures);

    public void TlsHandshakeTimedOut() => Interlocked.Increment(ref _tlsHandshakeTimeouts);

    public void TlsNoCertificateForSni() => Interlocked.Increment(ref _tlsNoCertificateForSniFailures);

    public ProxyMetricsSnapshot Snapshot()
    {
        return new ProxyMetricsSnapshot(
            Interlocked.Read(ref _acceptedConnections),
            Interlocked.Read(ref _activeConnections),
            Interlocked.Read(ref _totalRequests),
            Interlocked.Read(ref _upstreamSuccesses),
            Interlocked.Read(ref _upstreamFailures),
            Interlocked.Read(ref _bytesRead),
            Interlocked.Read(ref _bytesWritten),
            Interlocked.Read(ref _parseErrors),
            Interlocked.Read(ref _rejectedMalformedRequests),
            Interlocked.Read(ref _rejectedUnsupportedRequestFraming),
            Interlocked.Read(ref _upstreamMalformedResponses),
            Interlocked.Read(ref _clientBodyRelayFailures),
            Interlocked.Read(ref _upstreamBodyRelayFailures),
            Interlocked.Read(ref _clientRequestHeadTimeouts),
            Interlocked.Read(ref _clientRequestBodyTimeouts),
            Interlocked.Read(ref _upstreamConnectFailures),
            Interlocked.Read(ref _upstreamConnectTimeouts),
            Interlocked.Read(ref _upstreamResponseHeadTimeouts),
            Interlocked.Read(ref _upstreamResponseBodyTimeouts),
            Interlocked.Read(ref _upstreamPrematureDisconnects),
            Interlocked.Read(ref _clientPrematureDisconnects),
            Interlocked.Read(ref _proxyGenerated502Responses),
            Interlocked.Read(ref _proxyGenerated504Responses),
            Interlocked.Read(ref _downstreamWriteTimeouts),
            Interlocked.Read(ref _tlsHandshakeAttempts),
            Interlocked.Read(ref _tlsHandshakeSuccesses),
            Interlocked.Read(ref _tlsHandshakeFailures),
            Interlocked.Read(ref _tlsHandshakeTimeouts),
            Interlocked.Read(ref _tlsNoCertificateForSniFailures));
    }
}
