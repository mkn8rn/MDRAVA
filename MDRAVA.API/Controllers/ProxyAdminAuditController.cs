using Microsoft.AspNetCore.Mvc;
using BusinessProxyAdminAuditAdministrationService =
    MDRAVA.BLL.ControlPlane.AdminAudit.ProxyAdminAuditAdministrationService;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/audit")]
public sealed class ProxyAdminAuditController : ControllerBase
{
    private readonly BusinessProxyAdminAuditAdministrationService _auditAdministration;

    public ProxyAdminAuditController(BusinessProxyAdminAuditAdministrationService auditAdministration)
    {
        _auditAdministration = auditAdministration;
    }

    [HttpGet("recent")]
    public IReadOnlyList<ProxyAdminAuditEventResponse> Recent([FromQuery] int limit = 50)
    {
        return ProxyAdminAuditEventResponse.FromEvents(_auditAdministration.Recent(limit));
    }
}
