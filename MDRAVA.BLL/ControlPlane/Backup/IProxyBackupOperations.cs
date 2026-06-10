namespace MDRAVA.BLL.ControlPlane.Backup;

public interface IProxyBackupOperations
{
    ProxyBackupManifestResponse CreateManifest();

    ValueTask<ProxyRestoreValidationResponse> ValidateAsync(CancellationToken cancellationToken);
}
