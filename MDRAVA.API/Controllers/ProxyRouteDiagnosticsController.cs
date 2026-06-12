using MDRAVA.BLL.ControlPlane.RouteDiagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/routes")]
public sealed class ProxyRouteDiagnosticsController : ControllerBase
{
    private readonly ProxyRouteDiagnosticsAdministrationService _routeDiagnosticsAdministration;

    public ProxyRouteDiagnosticsController(ProxyRouteDiagnosticsAdministrationService routeDiagnosticsAdministration)
    {
        _routeDiagnosticsAdministration = routeDiagnosticsAdministration;
    }

    [HttpPost("match")]
    public ActionResult<RouteMatchDryRunResult> Match([FromBody] ProxyRouteMatchDryRunRequest? request)
    {
        var result = _routeDiagnosticsAdministration.Match(request?.ToRouteMatchDryRunRequest());
        return ProxyAdminHttpResultMapper.OkOrBadRequest(this, result, result.Succeeded);
    }
}
