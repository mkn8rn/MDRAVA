namespace MDRAVA.BLL.ControlPlane.Backup;

public interface IProxyBackupOperations
{
    ProxyBackupManifest CreateManifest();

    ValueTask<ProxyRestoreValidationResponse> ValidateAsync(CancellationToken cancellationToken);
}
