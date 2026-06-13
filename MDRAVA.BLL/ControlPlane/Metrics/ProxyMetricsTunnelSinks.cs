namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class ProxyMetrics
{
    public ProxyTunnelAdmissionDecision StartTunnel(int maxActiveTunnels)
    {
        while (true)
        {
            var observed = Interlocked.Read(ref _activeTunnels);
            if (observed >= maxActiveTunnels)
            {
                return ProxyTunnelAdmissionDecision.Rejected;
            }

            if (Interlocked.CompareExchange(ref _activeTunnels, observed + 1, observed) == observed)
            {
                return ProxyTunnelAdmissionDecision.Accepted;
            }
        }
    }

    public void TunnelStarted() => Interlocked.Increment(ref _totalTunnels);

    public void TunnelClosed() => Interlocked.Decrement(ref _activeTunnels);

    public void TunnelIdleTimedOut() => Interlocked.Increment(ref _tunnelIdleTimeouts);

    public void AddTunnelBytesClientToUpstream(long bytes)
    {
        if (bytes > 0)
        {
            Interlocked.Add(ref _tunnelBytesClientToUpstream, bytes);
        }
    }

    public void AddTunnelBytesUpstreamToClient(long bytes)
    {
        if (bytes > 0)
        {
            Interlocked.Add(ref _tunnelBytesUpstreamToClient, bytes);
        }
    }

    public void TunnelRelayFailed() => Interlocked.Increment(ref _tunnelRelayFailures);
}
