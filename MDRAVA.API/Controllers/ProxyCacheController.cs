using MDRAVA.BLL.ControlPlane.Caching;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/cache")]
public sealed class ProxyCacheController : ControllerBase
{
    private readonly ProxyCacheAdministrationService _cacheAdministration;

    public ProxyCacheController(ProxyCacheAdministrationService cacheAdministration)
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
