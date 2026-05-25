namespace MDRAVA.BLL.ControlPlane;

public interface IProxyConfigurationReloadOperations<TProjection>
    where TProjection : class
{
    ValueTask<ProxyConfigurationReloadResult<TProjection>> ReloadAsync(CancellationToken cancellationToken);
}
