namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyUpstreamSelectionSnapshot(
    string Route,
    string Upstream,
    string Scheme,
    string Protocol,
    long Count);
