namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed record ProxyRouteDiagnosticsRequestInput(
    string Scheme,
    string? Protocol,
    string? ListenerName,
    int? Port,
    string Target,
    string Path,
    ProxyRouteDiagnosticsRequestHead RequestHead,
    bool IsUpgradeRequest,
    List<RouteMatchDryRunFinding> Findings);
