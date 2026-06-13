using Microsoft.AspNetCore.Mvc;
using BusinessProxyAcmeAdministrationService = MDRAVA.BLL.ControlPlane.Acme.ProxyAcmeAdministrationService;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/acme")]
public sealed class ProxyAcmeController : ControllerBase
{
    private readonly BusinessProxyAcmeAdministrationService _acmeAdministration;

    public ProxyAcmeController(BusinessProxyAcmeAdministrationService acmeAdministration)
    {
        _acmeAdministration = acmeAdministration;
    }

    [HttpGet("status")]
    public ActionResult<AcmeStatusResponse> Status()
    {
        var status = _acmeAdministration.GetStatus();

        return ProxyAdminHttpResultMapper.OkOrNotFound(
            this,
            status is null ? null : AcmeStatusResponse.FromStatus(status));
    }
}
