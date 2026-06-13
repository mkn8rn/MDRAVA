using Microsoft.AspNetCore.Mvc;
using BusinessProxyRouteDiagnosticsAdministrationService =
    MDRAVA.BLL.ControlPlane.RouteDiagnostics.ProxyRouteDiagnosticsAdministrationService;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/routes")]
public sealed class ProxyRouteDiagnosticsController : ControllerBase
{
    private readonly BusinessProxyRouteDiagnosticsAdministrationService _routeDiagnosticsAdministration;

    public ProxyRouteDiagnosticsController(BusinessProxyRouteDiagnosticsAdministrationService routeDiagnosticsAdministration)
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
