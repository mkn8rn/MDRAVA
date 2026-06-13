using Microsoft.AspNetCore.Mvc;
using BusinessProxyCacheAdministrationService = MDRAVA.BLL.ControlPlane.Caching.ProxyCacheAdministrationService;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/cache")]
public sealed class ProxyCacheController : ControllerBase
{
    private readonly BusinessProxyCacheAdministrationService _cacheAdministration;

    public ProxyCacheController(BusinessProxyCacheAdministrationService cacheAdministration)
    {
        _cacheAdministration = cacheAdministration;
    }

    [HttpGet("status")]
    public ProxyCacheStatusResponse Status()
    {
        var status = _cacheAdministration.GetStatus();

        return ProxyCacheStatusResponse.FromStatus(status);
    }

    [HttpPost("clear")]
    public ProxyCacheStatusResponse Clear()
    {
        var status = _cacheAdministration.Clear();

        return ProxyCacheStatusResponse.FromStatus(status);
    }
}
