namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeTimeouts(
    TimeSpan ClientRequestHeadTimeout,
    TimeSpan ClientRequestBodyIdleTimeout,
    TimeSpan UpstreamConnectTimeout,
    TimeSpan UpstreamResponseHeadTimeout,
    TimeSpan UpstreamResponseBodyIdleTimeout,
    TimeSpan DownstreamWriteTimeout,
    TimeSpan TlsHandshakeTimeout,
    TimeSpan ClientKeepAliveIdleTimeout,
    TimeSpan UpstreamIdleConnectionLifetime,
    TimeSpan TunnelIdleTimeout);

public static class RuntimeTimeoutsFactory
{
    public static RuntimeTimeouts ForHealthCheck(TimeSpan timeout)
    {
        return new RuntimeTimeouts(
            timeout,
            timeout,
            timeout,
            timeout,
            timeout,
            timeout,
            timeout,
            timeout,
            timeout,
            timeout);
    }
}
