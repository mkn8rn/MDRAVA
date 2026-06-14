using BusinessRuntimeConnectionLimitsProjection = MDRAVA.BLL.Configuration.RuntimeConnectionLimitsProjection;
using BusinessRuntimeForwardedHeadersOptions = MDRAVA.BLL.Configuration.RuntimeForwardedHeadersOptions;
using BusinessRuntimeLimitsProjection = MDRAVA.BLL.Configuration.RuntimeLimitsProjection;
using BusinessRuntimeTimeouts = MDRAVA.BLL.Configuration.RuntimeTimeouts;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeTimeoutsResponse(
    TimeSpan ClientRequestHeadTimeout,
    TimeSpan ClientRequestBodyIdleTimeout,
    TimeSpan UpstreamConnectTimeout,
    TimeSpan UpstreamResponseHeadTimeout,
    TimeSpan UpstreamResponseBodyIdleTimeout,
    TimeSpan DownstreamWriteTimeout,
    TimeSpan TlsHandshakeTimeout,
    TimeSpan ClientKeepAliveIdleTimeout,
    TimeSpan UpstreamIdleConnectionLifetime,
    TimeSpan TunnelIdleTimeout)
{
    public static RuntimeTimeoutsResponse FromTimeouts(BusinessRuntimeTimeouts timeouts)
    {
        ArgumentNullException.ThrowIfNull(timeouts);

        return new RuntimeTimeoutsResponse(
            timeouts.ClientRequestHeadTimeout,
            timeouts.ClientRequestBodyIdleTimeout,
            timeouts.UpstreamConnectTimeout,
            timeouts.UpstreamResponseHeadTimeout,
            timeouts.UpstreamResponseBodyIdleTimeout,
            timeouts.DownstreamWriteTimeout,
            timeouts.TlsHandshakeTimeout,
            timeouts.ClientKeepAliveIdleTimeout,
            timeouts.UpstreamIdleConnectionLifetime,
            timeouts.TunnelIdleTimeout);
    }
}

public sealed record RuntimeConnectionLimitsResponse(
    int MaxRequestsPerClientConnection,
    int MaxIdleUpstreamConnectionsPerUpstream,
    int MaxActiveUpgradedTunnels)
{
    public static RuntimeConnectionLimitsResponse FromProjection(BusinessRuntimeConnectionLimitsProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeConnectionLimitsResponse(
            projection.MaxRequestsPerClientConnection,
            projection.MaxIdleUpstreamConnectionsPerUpstream,
            projection.MaxActiveUpgradedTunnels);
    }
}

public sealed record RuntimeLimitsResponse(
    int MaxActiveClientConnections,
    int MaxConcurrentTlsHandshakes,
    int RequestsPerMinutePerIp,
    int UpgradeRequestsPerMinutePerIp,
    int MaxRequestHeadBytes,
    int MaxHeaderCount,
    int MaxHeaderLineBytes,
    long MaxRequestBodyBytes,
    int MaxPathBytes,
    TimeSpan ShutdownGracePeriod)
{
    public static RuntimeLimitsResponse FromProjection(BusinessRuntimeLimitsProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeLimitsResponse(
            projection.MaxActiveClientConnections,
            projection.MaxConcurrentTlsHandshakes,
            projection.RequestsPerMinutePerIp,
            projection.UpgradeRequestsPerMinutePerIp,
            projection.MaxRequestHeadBytes,
            projection.MaxHeaderCount,
            projection.MaxHeaderLineBytes,
            projection.MaxRequestBodyBytes,
            projection.MaxPathBytes,
            projection.ShutdownGracePeriod);
    }
}

public sealed record RuntimeForwardedHeadersResponse(
    bool Enabled,
    IReadOnlyList<string> TrustedProxies)
{
    public static RuntimeForwardedHeadersResponse FromOptions(BusinessRuntimeForwardedHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RuntimeForwardedHeadersResponse(options.Enabled, options.TrustedProxies.ToArray());
    }
}
