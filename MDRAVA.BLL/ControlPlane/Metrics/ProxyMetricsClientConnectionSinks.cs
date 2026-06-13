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

    public void ClientPrematureDisconnect() => Interlocked.Increment(ref _clientPrematureDisconnects);

    public void ClientConnectionClosedByIdleTimeout() => Interlocked.Increment(ref _clientConnectionsClosedByIdleTimeout);

    public void ClientConnectionClosedByMaxRequests() => Interlocked.Increment(ref _clientConnectionsClosedByMaxRequests);
}
