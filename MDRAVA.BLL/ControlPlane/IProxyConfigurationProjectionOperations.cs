namespace MDRAVA.BLL.ControlPlane;

public interface IProxyConfigurationProjectionOperations<TProjection>
    where TProjection : class
{
    ProxyConfigurationProjectionReadResult<TProjection> GetActive();

    ProxyConfigurationProjectionReadResult<TProjection> GetEffective();
}
