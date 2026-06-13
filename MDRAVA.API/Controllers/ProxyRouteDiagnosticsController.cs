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
    public ActionResult<RouteMatchDryRunResponse> Match([FromBody] ProxyRouteMatchDryRunRequest? request)
    {
        var result = _routeDiagnosticsAdministration.Match(request?.ToRouteMatchDryRunRequest());
        var response = RouteMatchDryRunResponse.FromResult(result);

        return ProxyAdminHttpResultMapper.OkOrBadRequest(this, response, response.Succeeded);
    }
}
