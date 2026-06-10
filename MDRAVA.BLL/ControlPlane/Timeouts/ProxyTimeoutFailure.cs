using MDRAVA.BLL.ControlPlane;

namespace MDRAVA.BLL.ControlPlane.Timeouts;

public sealed record ProxyTimeoutFailure(
    int? ResponseStatusCode,
    ProxyFailureKind FailureKind);
