using MDRAVA.BLL.ControlPlane.Backup;
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
        var manifest = _backupAdministration.CreateManifest();

        return ProxyBackupManifestResponse.FromManifest(manifest);
    }

    [HttpPost("validate")]
    public async ValueTask<ActionResult<ProxyRestoreValidationResponseBody>> Validate(CancellationToken cancellationToken)
    {
        var result = await _backupAdministration.ValidateAsync(cancellationToken);
        var response = ProxyRestoreValidationResponseBody.FromResult(result);

        return ProxyAdminHttpResultMapper.OkOrBadRequest(this, response, response.Succeeded);
    }
}
