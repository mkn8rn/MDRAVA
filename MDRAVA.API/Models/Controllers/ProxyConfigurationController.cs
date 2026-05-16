using MDRAVA.API.Proxy.Configuration.Loading;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Configuration.Storage;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/config")]
public sealed class ProxyConfigurationController : ControllerBase
{
    private readonly IProxyConfigurationReloadService _reloadService;
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyConfigurationController(
        IProxyConfigurationReloadService reloadService,
        IProxyConfigurationStore configurationStore)
    {
        _reloadService = reloadService;
        _configurationStore = configurationStore;
    }

    [HttpPost("reload")]
    public async ValueTask<ActionResult<ProxyConfigurationReloadResult>> Reload(CancellationToken cancellationToken)
    {
        var result = await _reloadService.ReloadAsync(cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    [HttpPost("validate")]
    public async ValueTask<ActionResult<ProxyConfigurationValidationResult>> Validate(CancellationToken cancellationToken)
    {
        var result = await _reloadService.ValidateAsync(cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    [HttpGet("active")]
    public ActionResult<ProxyConfigurationProjection> Active()
    {
        if (!_configurationStore.TryGetSnapshot(out var snapshot) || snapshot is null)
        {
            return NotFound();
        }

        return Ok(ProxyConfigurationMapper.ToProjection(snapshot));
    }

    [HttpGet("effective")]
    public ActionResult<ProxyConfigurationProjection> Effective()
    {
        if (!_configurationStore.TryGetSnapshot(out var snapshot) || snapshot is null)
        {
            return NotFound();
        }

        return Ok(ProxyConfigurationMapper.ToProjection(snapshot));
    }
}
