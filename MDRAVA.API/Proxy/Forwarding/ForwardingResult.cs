using MDRAVA.API.Proxy.Observability;

namespace MDRAVA.API.Proxy.Forwarding;

public sealed record ForwardingResult(
    bool Succeeded,
    bool ResponseStarted,
    bool KeepClientConnectionOpen,
    int? ResponseStatusCode = null,
    ProxyFailureKind FailureKind = ProxyFailureKind.None,
    TunnelRelayResult? Tunnel = null);
