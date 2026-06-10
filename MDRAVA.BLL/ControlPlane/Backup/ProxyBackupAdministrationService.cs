namespace MDRAVA.BLL.ControlPlane.Backup;

public sealed class ProxyBackupAdministrationService
{
    private readonly IProxyBackupOperations _backupOperations;

    public ProxyBackupAdministrationService(IProxyBackupOperations backupOperations)
    {
        _backupOperations = backupOperations;
    }

    public ProxyBackupManifestResponse CreateManifest()
    {
        return _backupOperations.CreateManifest();
    }

    public ValueTask<ProxyRestoreValidationResponse> ValidateAsync(CancellationToken cancellationToken)
    {
        return _backupOperations.ValidateAsync(cancellationToken);
    }
}
