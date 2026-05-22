using MDRAVA.API.Proxy.Configuration.Loading;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Configuration.Storage;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/config")]
public sealed class ProxyConfigurationController : ControllerBase
{
    private readonly ProxyConfigurationAdministrationService _configurationAdministration;
    private readonly IProxyConfigurationReloadService _reloadService;
    private readonly IProxyConfigurationStore _configurationStore;

    public ProxyConfigurationController(
        ProxyConfigurationAdministrationService configurationAdministration,
        IProxyConfigurationReloadService reloadService,
        IProxyConfigurationStore configurationStore)
    {
        _configurationAdministration = configurationAdministration;
        _reloadService = reloadService;
        _configurationStore = configurationStore;
    }

    [HttpPost("normalize")]
    public ActionResult<ProxyConfigurationNormalizeResult> Normalize([FromBody] ProxyConfigurationNormalizeRequest request)
    {
        var result = _configurationAdministration.Normalize(request);
        return result.Succeeded ? Ok(result) : BadRequest(result);
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
        var result = await _configurationAdministration.ValidateAsync(cancellationToken);
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
