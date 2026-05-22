namespace MDRAVA.BLL.ControlPlane;

public sealed record RouteMatchDryRunPolicy(
    bool Enabled,
    bool WouldApply,
    string Reason);
