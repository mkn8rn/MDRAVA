namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public interface IProxyRouteDiagnosticsOperations
{
    RouteMatchDryRunResult Explain(RouteMatchDryRunRequest? request);
}
