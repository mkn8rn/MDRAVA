using MDRAVA.BLL.ControlPlane;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/audit")]
public sealed class ProxyAdminAuditController : ControllerBase
{
    private readonly ProxyAdminAuditAdministrationService _auditAdministration;

    public ProxyAdminAuditController(ProxyAdminAuditAdministrationService auditAdministration)
    {
        _auditAdministration = auditAdministration;
    }

    [HttpGet("recent")]
    public IReadOnlyList<ProxyAdminAuditEvent> Recent([FromQuery] int limit = 50)
    {
        return _auditAdministration.Recent(limit);
    }
}
