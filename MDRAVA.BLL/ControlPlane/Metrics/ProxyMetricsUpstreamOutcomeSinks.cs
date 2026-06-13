namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class ProxyMetrics
{
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

    public void UpstreamPrematureDisconnect() => Interlocked.Increment(ref _upstreamPrematureDisconnects);

    public void GeneratedFailureResponse(int statusCode)
    {
        if (statusCode == 502)
        {
            ProxyGenerated502();
        }
        else if (statusCode == 504)
        {
            ProxyGenerated504();
        }
    }

    private void ProxyGenerated502() => Interlocked.Increment(ref _proxyGenerated502Responses);

    private void ProxyGenerated504() => Interlocked.Increment(ref _proxyGenerated504Responses);

    public void UpstreamRequestFailed() => Interlocked.Increment(ref _upstreamRequestFailures);
}
