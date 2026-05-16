namespace MDRAVA.API.Proxy.Forwarding;

public sealed record TunnelRelayResult(
    string CloseReason,
    long BytesClientToUpstream,
    long BytesUpstreamToClient,
    TimeSpan Duration);
