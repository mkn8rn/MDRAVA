using Microsoft.AspNetCore.Mvc;
using BusinessProxyConfigurationAdministrationService =
    MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationAdministrationService;
using BusinessProxyConfigurationProjection =
    MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationProjection;
using BusinessProxyConfigurationReadAdministrationService =
    MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationReadAdministrationService<MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationProjection>;
using BusinessProxyConfigurationReadResult =
    MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationReadResult<MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationProjection>;
using BusinessProxyConfigurationReloadAdministrationService =
    MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationReloadAdministrationService<MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationProjection>;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/config")]
public sealed class ProxyConfigurationController : ControllerBase
{
    private readonly BusinessProxyConfigurationAdministrationService _configurationAdministration;
    private readonly BusinessProxyConfigurationReadAdministrationService _configurationReads;
    private readonly BusinessProxyConfigurationReloadAdministrationService _configurationReloads;

    public ProxyConfigurationController(
        BusinessProxyConfigurationAdministrationService configurationAdministration,
        BusinessProxyConfigurationReadAdministrationService configurationReads,
        BusinessProxyConfigurationReloadAdministrationService configurationReloads)
    {
        _configurationAdministration = configurationAdministration;
        _configurationReads = configurationReads;
        _configurationReloads = configurationReloads;
    }

    [HttpPost("normalize")]
    public ActionResult<ProxyConfigurationNormalizeResponse> Normalize([FromBody] ProxyConfigurationNormalizeSubmissionRequest? request)
    {
        var result = _configurationAdministration.Normalize(request?.ToNormalizeRequest());
        var response = ProxyConfigurationNormalizeResponse.FromResult(result);
        return ProxyAdminHttpResultMapper.OkOrBadRequest(this, response, response.Succeeded);
    }

    [HttpPost("reload")]
    public async ValueTask<ActionResult<ProxyConfigurationReloadResponse<BusinessProxyConfigurationProjection>>> Reload(
        CancellationToken cancellationToken)
    {
        var result = await _configurationReloads.ReloadAsync(cancellationToken);
        var response = ProxyConfigurationReloadResponse<BusinessProxyConfigurationProjection>.FromResult(result);
        return ProxyAdminHttpResultMapper.OkOrBadRequest(this, response, response.Succeeded);
    }

    [HttpPost("validate")]
    public async ValueTask<ActionResult<ProxyConfigurationValidationResponse>> Validate(CancellationToken cancellationToken)
    {
        var result = await _configurationAdministration.ValidateAsync(cancellationToken);
        var response = ProxyConfigurationValidationResponse.FromResult(result);
        return ProxyAdminHttpResultMapper.OkOrBadRequest(this, response, response.Succeeded);
    }

    [HttpGet("active")]
    public ActionResult<BusinessProxyConfigurationProjection> Active()
    {
        var result = _configurationReads.ReadActive();
        return result is BusinessProxyConfigurationReadResult.AvailableResult available
            ? Ok(available.Configuration)
            : NotFound();
    }

    [HttpGet("effective")]
    public ActionResult<BusinessProxyConfigurationProjection> Effective()
    {
        var result = _configurationReads.ReadEffective();
        return result is BusinessProxyConfigurationReadResult.AvailableResult available
            ? Ok(available.Configuration)
            : NotFound();
    }
}
