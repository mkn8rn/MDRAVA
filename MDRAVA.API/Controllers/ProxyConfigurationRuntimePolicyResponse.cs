using BusinessRuntimeConnectionLimitsProjection = MDRAVA.BLL.Configuration.RuntimeConnectionLimitsProjection;
using BusinessRuntimeForwardedHeadersProjection = MDRAVA.BLL.Configuration.RuntimeForwardedHeadersProjection;
using BusinessRuntimeLimitsProjection = MDRAVA.BLL.Configuration.RuntimeLimitsProjection;
using BusinessRuntimeTimeoutsProjection = MDRAVA.BLL.Configuration.RuntimeTimeoutsProjection;

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
    public static RuntimeTimeoutsResponse FromProjection(BusinessRuntimeTimeoutsProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeTimeoutsResponse(
            projection.ClientRequestHeadTimeout,
            projection.ClientRequestBodyIdleTimeout,
            projection.UpstreamConnectTimeout,
            projection.UpstreamResponseHeadTimeout,
            projection.UpstreamResponseBodyIdleTimeout,
            projection.DownstreamWriteTimeout,
            projection.TlsHandshakeTimeout,
            projection.ClientKeepAliveIdleTimeout,
            projection.UpstreamIdleConnectionLifetime,
            projection.TunnelIdleTimeout);
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
    public static RuntimeForwardedHeadersResponse FromProjection(BusinessRuntimeForwardedHeadersProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeForwardedHeadersResponse(projection.Enabled, projection.TrustedProxies.ToArray());
    }
}
