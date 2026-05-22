namespace MDRAVA.BLL.ControlPlane;

public sealed record RouteMatchDryRunUpstream(
    string Name,
    string Scheme,
    string Protocol,
    string Endpoint,
    int Weight,
    string SelectionReason);
