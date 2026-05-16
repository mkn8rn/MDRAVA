using MDRAVA.API.Proxy.Security;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/audit")]
public sealed class ProxyAdminAuditController : ControllerBase
{
    private readonly AdminAuditStore _auditStore;

    public ProxyAdminAuditController(AdminAuditStore auditStore)
    {
        _auditStore = auditStore;
    }

    [HttpGet("recent")]
    public IReadOnlyList<AdminAuditEvent> Recent([FromQuery] int limit = 50)
    {
        return _auditStore.Recent(limit);
    }
}
