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
        return _cacheAdministration.GetStatus();
    }

    [HttpPost("clear")]
    public ProxyCacheStatusResponse Clear()
    {
        return _cacheAdministration.Clear();
    }
}
