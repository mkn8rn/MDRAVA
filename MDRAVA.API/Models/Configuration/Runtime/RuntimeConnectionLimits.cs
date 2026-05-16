namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeConnectionLimits(
    int MaxRequestsPerClientConnection,
    int MaxIdleUpstreamConnectionsPerUpstream,
    int MaxActiveUpgradedTunnels);
