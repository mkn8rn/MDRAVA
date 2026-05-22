namespace MDRAVA.BLL.ControlPlane;

public sealed record RouteMatchDryRunListener(
    string Name,
    string Transport,
    string Address,
    int Port,
    string Protocols);
