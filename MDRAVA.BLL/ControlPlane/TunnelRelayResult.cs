namespace MDRAVA.BLL.ControlPlane;

public sealed record TunnelRelayResult(
    string CloseReason,
    long BytesClientToUpstream,
    long BytesUpstreamToClient,
    TimeSpan Duration);
