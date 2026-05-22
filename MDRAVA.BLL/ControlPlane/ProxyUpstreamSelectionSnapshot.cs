namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyUpstreamSelectionSnapshot(
    string Route,
    string Upstream,
    string Scheme,
    string Protocol,
    long Count);
