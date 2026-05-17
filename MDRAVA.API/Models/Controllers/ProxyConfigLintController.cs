using MDRAVA.API.Proxy.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/config/lint")]
public sealed class ProxyConfigLintController : ControllerBase
{
    private readonly ConfigLintService _lintService;

    public ProxyConfigLintController(ConfigLintService lintService)
    {
        _lintService = lintService;
    }

    [HttpGet]
    public ActionResult<ConfigLintResult> Active()
    {
        var result = _lintService.LintActive();
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    [HttpPost]
    public ActionResult<ConfigLintResult> Submitted([FromBody] ConfigLintRequest request)
    {
        var result = _lintService.LintSubmitted(request);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }
}
