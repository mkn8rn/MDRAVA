using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/acme")]
public sealed class ProxyAcmeController : ControllerBase
{
    private readonly ProxyAcmeAdministrationService _acmeAdministration;

    public ProxyAcmeController(ProxyAcmeAdministrationService acmeAdministration)
    {
        _acmeAdministration = acmeAdministration;
    }

    [HttpGet("status")]
    public ActionResult<AcmeStatusResponse> Status()
    {
        return ProxyAdminHttpResultMapper.OkOrNotFound(this, _acmeAdministration.GetStatus());
    }
}
