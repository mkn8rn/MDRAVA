using MDRAVA.BLL.ControlPlane.Forwarding;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class ProxyMetrics
{
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

    public void RequestCompleted(string? site, string? route, string? action, int? statusCode)
    {
        var key = new RequestSeriesKey(
            ProxyMetricLabelPolicy.NormalizeValue(site),
            ProxyMetricLabelPolicy.NormalizeValue(route),
            ProxyMetricLabelPolicy.NormalizeValue(action),
            ProxyMetricLabelPolicy.StatusClass(statusCode));
        var counter = _requestsByRoute.GetOrAdd(key, static _ => new RequestSeriesCounter());
        Interlocked.Increment(ref counter.Count);
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

    public void ClientConnectionClosedByIdleTimeout() => Interlocked.Increment(ref _clientConnectionsClosedByIdleTimeout);

    public void ClientConnectionClosedByMaxRequests() => Interlocked.Increment(ref _clientConnectionsClosedByMaxRequests);

    public void UpstreamRequestFailed() => Interlocked.Increment(ref _upstreamRequestFailures);

    public void RequestFailed(ProxyFailureKind failureKind)
    {
        if (failureKind == ProxyFailureKind.None)
        {
            return;
        }

        var index = (int)failureKind;
        if ((uint)index < (uint)_requestFailuresByKind.Length)
        {
            Interlocked.Increment(ref _requestFailuresByKind[index]);
        }
    }
}
