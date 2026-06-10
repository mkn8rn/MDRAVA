using MDRAVA.BLL.ControlPlane.Forwarding;

namespace MDRAVA.BLL.ControlPlane.Timeouts;

public sealed record ProxyTimeoutFailure(
    int? ResponseStatusCode,
    ProxyFailureKind FailureKind);
