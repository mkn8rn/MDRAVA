using MDRAVA.API.Proxy.Observability;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/diagnostics")]
public sealed class ProxyDiagnosticsController : ControllerBase
{
    private readonly RecentRequestDiagnosticsStore _diagnostics;

    public ProxyDiagnosticsController(RecentRequestDiagnosticsStore diagnostics)
    {
        _diagnostics = diagnostics;
    }

    [HttpGet("recent")]
    public IReadOnlyList<ProxyRequestDiagnosticEvent> Recent([FromQuery] int limit = 50)
    {
        return _diagnostics.Recent(limit);
    }
}
