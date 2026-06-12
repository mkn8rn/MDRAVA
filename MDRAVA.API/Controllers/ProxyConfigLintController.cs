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
    public ActionResult<ConfigLintResult> Active()
    {
        var result = _configLintAdministration.LintActive();
        return ProxyAdminHttpResultMapper.OkOrBadRequest(this, result, result.Succeeded);
    }

    [HttpPost]
    public ActionResult<ConfigLintResult> Submitted([FromBody] ConfigLintRequest? request)
    {
        var result = _configLintAdministration.LintSubmitted(request);
        return ProxyAdminHttpResultMapper.OkOrBadRequest(this, result, result.Succeeded);
    }
}
