namespace MDRAVA.BLL.ControlPlane.Forwarding;

public sealed record ForwardingResult(
    bool Succeeded,
    bool ResponseStarted,
    bool KeepClientConnectionOpen,
    int? ResponseStatusCode = null,
    ProxyFailureKind FailureKind = ProxyFailureKind.None,
    TunnelRelayResult? Tunnel = null);
