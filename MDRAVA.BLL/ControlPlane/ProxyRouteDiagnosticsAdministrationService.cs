namespace MDRAVA.BLL.ControlPlane;

public sealed class ProxyRouteDiagnosticsAdministrationService
{
    private readonly IProxyRouteDiagnosticsOperations _operations;

    public ProxyRouteDiagnosticsAdministrationService(IProxyRouteDiagnosticsOperations operations)
    {
        _operations = operations;
    }

    public RouteMatchDryRunResult Match(RouteMatchDryRunRequest request)
    {
        return _operations.Explain(request);
    }
}
