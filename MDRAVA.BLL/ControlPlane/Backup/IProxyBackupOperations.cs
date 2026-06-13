namespace MDRAVA.BLL.ControlPlane.Backup;

public interface IProxyBackupOperations
{
    ProxyBackupManifest CreateManifest();

    ValueTask<ProxyRestoreValidationResult> ValidateAsync(CancellationToken cancellationToken);
}
