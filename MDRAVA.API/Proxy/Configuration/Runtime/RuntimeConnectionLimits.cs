namespace MDRAVA.API.Proxy.Configuration.Runtime;

public sealed record RuntimeConnectionLimits(
    int MaxRequestsPerClientConnection,
    int MaxIdleUpstreamConnectionsPerUpstream,
    int MaxActiveUpgradedTunnels);
