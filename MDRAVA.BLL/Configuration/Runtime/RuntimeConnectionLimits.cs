namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeConnectionLimits(
    int MaxRequestsPerClientConnection,
    int MaxIdleUpstreamConnectionsPerUpstream,
    int MaxActiveUpgradedTunnels);
