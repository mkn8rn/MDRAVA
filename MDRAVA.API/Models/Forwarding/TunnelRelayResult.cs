namespace MDRAVA.API.Models.Forwarding;

public sealed record TunnelRelayResult(
    string CloseReason,
    long BytesClientToUpstream,
    long BytesUpstreamToClient,
    TimeSpan Duration);
