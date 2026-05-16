namespace MDRAVA.API.Proxy.Forwarding;

public sealed record ForwardingResult(
    bool Succeeded,
    bool ResponseStarted,
    bool KeepClientConnectionOpen);
