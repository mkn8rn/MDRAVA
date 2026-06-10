using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
using MDRAVA.BLL.Configuration;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/config")]
public sealed class ProxyConfigurationController : ControllerBase
{
    private readonly ProxyConfigurationAdministrationService _configurationAdministration;
    private readonly ProxyConfigurationReadAdministrationService<ProxyConfigurationProjection> _configurationReads;
    private readonly ProxyConfigurationReloadAdministrationService<ProxyConfigurationProjection> _configurationReloads;

    public ProxyConfigurationController(
        ProxyConfigurationAdministrationService configurationAdministration,
        ProxyConfigurationReadAdministrationService<ProxyConfigurationProjection> configurationReads,
        ProxyConfigurationReloadAdministrationService<ProxyConfigurationProjection> configurationReloads)
    {
        _configurationAdministration = configurationAdministration;
        _configurationReads = configurationReads;
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
        var result = _configurationReads.ReadActive();
        return ProxyAdminHttpResultMapper.OkOrNotFound(this, result.Found, result.Configuration);
    }

    [HttpGet("effective")]
    public ActionResult<ProxyConfigurationProjection> Effective()
    {
        var result = _configurationReads.ReadEffective();
        return ProxyAdminHttpResultMapper.OkOrNotFound(this, result.Found, result.Configuration);
    }
}
