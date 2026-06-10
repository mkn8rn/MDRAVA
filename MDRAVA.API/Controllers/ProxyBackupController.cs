using MDRAVA.BLL.ControlPlane;
using Microsoft.AspNetCore.Mvc;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/backup")]
public sealed class ProxyBackupController : ControllerBase
{
    private readonly ProxyBackupAdministrationService _backupAdministration;

    public ProxyBackupController(ProxyBackupAdministrationService backupAdministration)
    {
        _backupAdministration = backupAdministration;
    }

    [HttpGet("manifest")]
    public ProxyBackupManifestResponse Manifest()
    {
        return _backupAdministration.CreateManifest();
    }

    [HttpPost("validate")]
    public async ValueTask<ActionResult<ProxyRestoreValidationResponse>> Validate(CancellationToken cancellationToken)
    {
        var result = await _backupAdministration.ValidateAsync(cancellationToken);
        return ProxyAdminHttpResultMapper.OkOrBadRequest(this, result, result.Succeeded);
    }
}
