using MDRAVA.BLL.ControlPlane.ConfigLint;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/config/lint")]
public sealed class ProxyConfigLintController : ControllerBase
{
    private readonly ProxyConfigLintAdministrationService _configLintAdministration;

    public ProxyConfigLintController(ProxyConfigLintAdministrationService configLintAdministration)
    {
        _configLintAdministration = configLintAdministration;
    }

    [HttpGet]
    public ActionResult<ConfigLintResponse> Active()
    {
        var result = _configLintAdministration.LintActive();
        var response = ConfigLintResponse.FromResult(result);

        return ProxyAdminHttpResultMapper.OkOrBadRequest(this, response, response.Succeeded);
    }

    [HttpPost]
    public ActionResult<ConfigLintResponse> Submitted([FromBody] ProxyConfigLintSubmissionRequest? request)
    {
        var result = _configLintAdministration.LintSubmitted(request?.ToConfigLintRequest());
        var response = ConfigLintResponse.FromResult(result);

        return ProxyAdminHttpResultMapper.OkOrBadRequest(this, response, response.Succeeded);
    }
}
