namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyTimeoutFailure(
    int? ResponseStatusCode,
    ProxyFailureKind FailureKind);
