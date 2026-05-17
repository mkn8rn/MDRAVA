using MDRAVA.API.Proxy.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/routes")]
public sealed class ProxyRouteDiagnosticsController : ControllerBase
{
    private readonly RouteMatchDiagnosticsService _diagnostics;

    public ProxyRouteDiagnosticsController(RouteMatchDiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics;
    }

    [HttpPost("match")]
    public ActionResult<RouteMatchDryRunResult> Match([FromBody] RouteMatchDryRunRequest request)
    {
        var result = _diagnostics.Explain(request);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }
}
