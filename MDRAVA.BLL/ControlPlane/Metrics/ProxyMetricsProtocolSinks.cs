namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class ProxyMetrics
{
    public void Http2ConnectionAccepted() => Interlocked.Increment(ref _http2AcceptedConnections);

    public void Http2RequestReceived() => Interlocked.Increment(ref _http2Requests);

    public void Http2StreamStarted() => Interlocked.Increment(ref _activeHttp2Streams);

    public void Http2StreamEnded() => Interlocked.Decrement(ref _activeHttp2Streams);

    public void Http2ProtocolError(string reason)
    {
        var counter = _http2ProtocolErrors.GetOrAdd(ProxyMetricLabelPolicy.NormalizeValue(reason), static _ => new RequestSeriesCounter());
        Interlocked.Increment(ref counter.Count);
    }

    public void UpstreamHttp2RequestAttempted() => Interlocked.Increment(ref _upstreamHttp2Requests);

    public void UpstreamHttp2AlpnFailed() => Interlocked.Increment(ref _upstreamHttp2AlpnFailures);

    public void UpstreamHttp2ProtocolError() => Interlocked.Increment(ref _upstreamHttp2ProtocolErrors);

    public void UpstreamHttp3RequestAttempted() => Interlocked.Increment(ref _upstreamHttp3Requests);

    public void UpstreamHttp3ConnectionAttempted() => Interlocked.Increment(ref _upstreamHttp3ConnectionAttempts);

    public void UpstreamHttp3ConnectionSucceeded() => Interlocked.Increment(ref _upstreamHttp3ConnectionSuccesses);

    public void UpstreamHttp3ConnectionFailed() => Interlocked.Increment(ref _upstreamHttp3ConnectionFailures);

    public void UpstreamHttp3PoolConnectionOpened() => Interlocked.Increment(ref _upstreamHttp3PoolConnectionsOpened);

    public void UpstreamHttp3PoolConnectionReused() => Interlocked.Increment(ref _upstreamHttp3PoolConnectionsReused);

    public void UpstreamHttp3PoolConnectionClosed() => Interlocked.Increment(ref _upstreamHttp3PoolConnectionsClosed);

    public void UpstreamHttp3StreamLimitRejected() => Interlocked.Increment(ref _upstreamHttp3StreamLimitRejections);

    public void UpstreamHttp3ConnectionOpened() => Interlocked.Increment(ref _activeUpstreamHttp3Connections);

    public void UpstreamHttp3ConnectionClosed() => Interlocked.Decrement(ref _activeUpstreamHttp3Connections);

    public void UpstreamHttp3StreamStarted() => Interlocked.Increment(ref _activeUpstreamHttp3Streams);

    public void UpstreamHttp3StreamEnded() => Interlocked.Decrement(ref _activeUpstreamHttp3Streams);

    public void UpstreamHttp3ProtocolError(string reason)
    {
        var counter = _upstreamHttp3ProtocolErrors.GetOrAdd(ProxyMetricLabelPolicy.NormalizeValue(reason), static _ => new RequestSeriesCounter());
        Interlocked.Increment(ref counter.Count);
    }

    public void Http3ConnectionAccepted()
    {
        Interlocked.Increment(ref _http3AcceptedConnections);
        Interlocked.Increment(ref _activeHttp3Connections);
    }

    public void Http3ConnectionClosed() => Interlocked.Decrement(ref _activeHttp3Connections);

    public void Http3RequestReceived() => Interlocked.Increment(ref _http3Requests);

    public void Http3RequestCompleted(string? method, int? statusCode, string? outcome)
    {
        var key = new Http3OutcomeKey(
            ProxyMetricLabelPolicy.NormalizeValue(method),
            ProxyMetricLabelPolicy.NormalizeValue(outcome),
            ProxyMetricLabelPolicy.StatusClass(statusCode));
        var counter = _http3RequestsByOutcome.GetOrAdd(key, static _ => new RequestSeriesCounter());
        Interlocked.Increment(ref counter.Count);
    }

    public void Http3ProxiedRequest() => Interlocked.Increment(ref _http3ProxiedRequests);

    public void Http3GeneratedResponse() => Interlocked.Increment(ref _http3GeneratedResponses);

    public void Http3StreamStarted() => Interlocked.Increment(ref _activeHttp3Streams);

    public void Http3StreamEnded() => Interlocked.Decrement(ref _activeHttp3Streams);

    public void Http3StreamReset() => Interlocked.Increment(ref _http3StreamResets);

    public void Http3StreamedResponse() => Interlocked.Increment(ref _http3StreamedResponses);

    public void Http3ResponseStreamStarted() => Interlocked.Increment(ref _activeHttp3ResponseStreams);

    public void Http3ResponseStreamEnded() => Interlocked.Decrement(ref _activeHttp3ResponseStreams);

    public void AddHttp3ResponseBytesSent(long bytes)
    {
        if (bytes > 0)
        {
            Interlocked.Add(ref _http3ResponseBytesSent, bytes);
        }
    }

    public void AddHttp3RequestBodyBytesReceived(long bytes)
    {
        if (bytes > 0)
        {
            Interlocked.Add(ref _http3RequestBodyBytesReceived, bytes);
        }
    }

    public void Http3ResponseStreamReset() => Interlocked.Increment(ref _http3ResponseStreamResets);

    public void Http3AltSvcEmitted() => Interlocked.Increment(ref _http3AltSvcEmitted);

    public void Http3AltSvcSuppressed() => Interlocked.Increment(ref _http3AltSvcSuppressed);

    public void Http3RequestRejected(string reason)
    {
        var counter = _http3RejectedRequests.GetOrAdd(ProxyMetricLabelPolicy.NormalizeValue(reason), static _ => new RequestSeriesCounter());
        Interlocked.Increment(ref counter.Count);
    }

    public void Http3ProtocolError(string reason)
    {
        var counter = _http3ProtocolErrors.GetOrAdd(ProxyMetricLabelPolicy.NormalizeValue(reason), static _ => new RequestSeriesCounter());
        Interlocked.Increment(ref counter.Count);
    }

    public void QuicListenerStarted() => Interlocked.Increment(ref _quicListenerStartSuccesses);

    public void QuicListenerStartFailed() => Interlocked.Increment(ref _quicListenerStartFailures);

    public void SetActiveQuicListeners(long count) => Interlocked.Exchange(ref _activeQuicListeners, count);
}
