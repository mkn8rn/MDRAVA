namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeConnectionLimitsProjection(
    int MaxRequestsPerClientConnection,
    int MaxIdleUpstreamConnectionsPerUpstream,
    int MaxActiveUpgradedTunnels);
