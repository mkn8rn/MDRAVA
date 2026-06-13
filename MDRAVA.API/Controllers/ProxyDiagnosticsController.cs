using Microsoft.AspNetCore.Mvc;
using BusinessProxyDiagnosticsAdministrationService =
    MDRAVA.BLL.ControlPlane.RequestDiagnostics.ProxyDiagnosticsAdministrationService;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/diagnostics")]
public sealed class ProxyDiagnosticsController : ControllerBase
{
    private readonly BusinessProxyDiagnosticsAdministrationService _diagnosticsAdministration;

    public ProxyDiagnosticsController(BusinessProxyDiagnosticsAdministrationService diagnosticsAdministration)
    {
        _diagnosticsAdministration = diagnosticsAdministration;
    }

    [HttpGet("recent")]
    public IReadOnlyList<ProxyRecentRequestDiagnosticEventResponse> Recent([FromQuery] int limit = 50)
    {
        return ProxyRecentRequestDiagnosticEventResponse.FromEvents(_diagnosticsAdministration.Recent(limit));
    }
}
