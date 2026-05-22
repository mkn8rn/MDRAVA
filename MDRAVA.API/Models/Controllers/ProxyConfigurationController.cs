using MDRAVA.API.Proxy.Configuration.Loading;
using MDRAVA.API.Proxy.Configuration.Runtime;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/config")]
public sealed class ProxyConfigurationController : ControllerBase
{
    private readonly ProxyConfigurationAdministrationService _configurationAdministration;
    private readonly ProxyConfigurationProjectionAdministrationService<ProxyConfigurationProjection> _configurationProjections;
    private readonly IProxyConfigurationReloadService _reloadService;

    public ProxyConfigurationController(
        ProxyConfigurationAdministrationService configurationAdministration,
        ProxyConfigurationProjectionAdministrationService<ProxyConfigurationProjection> configurationProjections,
        IProxyConfigurationReloadService reloadService)
    {
        _configurationAdministration = configurationAdministration;
        _configurationProjections = configurationProjections;
        _reloadService = reloadService;
    }

    [HttpPost("normalize")]
    public ActionResult<ProxyConfigurationNormalizeResult> Normalize([FromBody] ProxyConfigurationNormalizeRequest request)
    {
        var result = _configurationAdministration.Normalize(request);
        return ProxyAdminHttpResultMapper.OkOrBadRequest(this, result, result.Succeeded);
    }

    [HttpPost("reload")]
    public async ValueTask<ActionResult<ProxyConfigurationReloadResult>> Reload(CancellationToken cancellationToken)
    {
        var result = await _reloadService.ReloadAsync(cancellationToken);
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
