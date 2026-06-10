namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public interface IProxyConfigurationReloadOperations<TProjection>
    where TProjection : class
{
    ValueTask<ProxyConfigurationReloadResult<TProjection>> ReloadAsync(CancellationToken cancellationToken);
}
