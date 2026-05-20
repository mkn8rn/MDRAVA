using MDRAVA.API.Proxy.Backup;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/backup")]
public sealed class ProxyBackupController : ControllerBase
{
    private readonly ProxyBackupReadinessService _backupReadiness;

    public ProxyBackupController(ProxyBackupReadinessService backupReadiness)
    {
        _backupReadiness = backupReadiness;
    }

    [HttpGet("manifest")]
    public ProxyBackupManifestResponse Manifest()
    {
        return _backupReadiness.CreateManifest();
    }

    [HttpPost("validate")]
    public async ValueTask<ActionResult<ProxyRestoreValidationResponse>> Validate(CancellationToken cancellationToken)
    {
        var result = await _backupReadiness.ValidateAsync(cancellationToken);
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }
}
