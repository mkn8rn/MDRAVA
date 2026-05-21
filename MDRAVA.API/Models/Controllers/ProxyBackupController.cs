using MDRAVA.API.Proxy.Backup;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/backup")]
public sealed class ProxyBackupController : ControllerBase
{
    private readonly ProxyBackupService _backupService;

    public ProxyBackupController(ProxyBackupService backupService)
    {
        _backupService = backupService;
    }

    [HttpGet("manifest")]
    public ProxyBackupManifestResponse Manifest()
    {
        return _backupService.CreateManifest();
    }

    [HttpPost("validate")]
    public async ValueTask<ActionResult<ProxyRestoreValidationResponse>> Validate(CancellationToken cancellationToken)
    {
        var result = await _backupService.ValidateAsync(cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }
}
