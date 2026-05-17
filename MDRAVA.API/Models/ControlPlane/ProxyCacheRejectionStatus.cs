namespace MDRAVA.API.Models.ControlPlane;

public sealed record ProxyCacheRejectionStatus(
    string Reason,
    long Count);
