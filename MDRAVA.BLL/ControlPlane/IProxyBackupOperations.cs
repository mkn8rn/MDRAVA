namespace MDRAVA.BLL.ControlPlane;

public interface IProxyBackupOperations
{
    ProxyBackupManifestResponse CreateManifest();

    ValueTask<ProxyRestoreValidationResponse> ValidateAsync(CancellationToken cancellationToken);
}
