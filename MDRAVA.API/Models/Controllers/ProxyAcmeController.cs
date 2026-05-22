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
        var response = _acmeAdministration.GetStatus();
        return response is null ? NotFound() : Ok(response);
    }
}
