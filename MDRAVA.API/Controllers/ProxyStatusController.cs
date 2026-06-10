using MDRAVA.BLL.ControlPlane;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy")]
public sealed class ProxyStatusController : ControllerBase
{
    private readonly ProxyStatusAdministrationService _statusAdministration;

    public ProxyStatusController(ProxyStatusAdministrationService statusAdministration)
    {
        _statusAdministration = statusAdministration;
    }

    [HttpGet("status")]
    public ProxyStatusResponse Get()
    {
        return _statusAdministration.GetStatus();
    }
}
