namespace MDRAVA.BLL.ControlPlane;

public interface IProxyRouteDiagnosticsOperations
{
    RouteMatchDryRunResult Explain(RouteMatchDryRunRequest? request);
}
