using MDRAVA.API.Proxy.Configuration.Runtime;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/config")]
public sealed class ProxyConfigurationController : ControllerBase
{
    private readonly ProxyConfigurationAdministrationService _configurationAdministration;
    private readonly ProxyConfigurationProjectionAdministrationService<ProxyConfigurationProjection> _configurationProjections;
    private readonly ProxyConfigurationReloadAdministrationService<ProxyConfigurationProjection> _configurationReloads;

    public ProxyConfigurationController(
        ProxyConfigurationAdministrationService configurationAdministration,
        ProxyConfigurationProjectionAdministrationService<ProxyConfigurationProjection> configurationProjections,
        ProxyConfigurationReloadAdministrationService<ProxyConfigurationProjection> configurationReloads)
    {
        _configurationAdministration = configurationAdministration;
        _configurationProjections = configurationProjections;
        _configurationReloads = configurationReloads;
    }

    [HttpPost("normalize")]
    public ActionResult<ProxyConfigurationNormalizeResult> Normalize([FromBody] ProxyConfigurationNormalizeRequest request)
    {
        var result = _configurationAdministration.Normalize(request);
        return ProxyAdminHttpResultMapper.OkOrBadRequest(this, result, result.Succeeded);
    }

    [HttpPost("reload")]
    public async ValueTask<ActionResult<ProxyConfigurationReloadResult<ProxyConfigurationProjection>>> Reload(
        CancellationToken cancellationToken)
    {
        var result = await _configurationReloads.ReloadAsync(cancellationToken);
        return ProxyAdminHttpResultMapper.OkOrBadRequest(this, result, result.Succeeded);
    }

    [HttpPost("validate")]
    public async ValueTask<ActionResult<ProxyConfigurationValidationResult>> Validate(CancellationToken cancellationToken)
    {
        var result = await _configurationAdministration.ValidateAsync(cancellationToken);
        return ProxyAdminHttpResultMapper.OkOrBadRequest(this, result, result.Succeeded);
    }

    [HttpGet("active")]
    public ActionResult<ProxyConfigurationProjection> Active()
    {
        var result = _configurationProjections.GetActive();
        return ProxyAdminHttpResultMapper.OkOrNotFound(this, result.Found, result.Projection);
    }

    [HttpGet("effective")]
    public ActionResult<ProxyConfigurationProjection> Effective()
    {
        var result = _configurationProjections.GetEffective();
        return ProxyAdminHttpResultMapper.OkOrNotFound(this, result.Found, result.Projection);
    }
}
