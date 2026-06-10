using MDRAVA.BLL.ControlPlane.RequestDiagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/diagnostics")]
public sealed class ProxyDiagnosticsController : ControllerBase
{
    private readonly ProxyDiagnosticsAdministrationService _diagnosticsAdministration;

    public ProxyDiagnosticsController(ProxyDiagnosticsAdministrationService diagnosticsAdministration)
    {
        _diagnosticsAdministration = diagnosticsAdministration;
    }

    [HttpGet("recent")]
    public IReadOnlyList<ProxyRecentRequestDiagnosticEvent> Recent([FromQuery] int limit = 50)
    {
        return _diagnosticsAdministration.Recent(limit);
    }
}
