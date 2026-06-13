using Microsoft.AspNetCore.Mvc;
using BusinessProxyBackupAdministrationService = MDRAVA.BLL.ControlPlane.Backup.ProxyBackupAdministrationService;

namespace MDRAVA.API.Controllers;

[ApiController]
[Route("admin/proxy/backup")]
public sealed class ProxyBackupController : ControllerBase
{
    private readonly BusinessProxyBackupAdministrationService _backupAdministration;

    public ProxyBackupController(BusinessProxyBackupAdministrationService backupAdministration)
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
