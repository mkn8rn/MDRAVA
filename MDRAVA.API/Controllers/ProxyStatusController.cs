using Microsoft.AspNetCore.Mvc;
using BusinessProxyStatusAdministrationService = MDRAVA.BLL.ControlPlane.Status.ProxyStatusAdministrationService;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy")]
public sealed class ProxyStatusController : ControllerBase
{
    private readonly BusinessProxyStatusAdministrationService _statusAdministration;

    public ProxyStatusController(BusinessProxyStatusAdministrationService statusAdministration)
    {
        _statusAdministration = statusAdministration;
    }

    [HttpGet("status")]
    public ProxyStatusResponse Get()
    {
        var status = _statusAdministration.GetStatus();

        return ProxyStatusResponse.FromBusinessResponse(status);
    }
}
