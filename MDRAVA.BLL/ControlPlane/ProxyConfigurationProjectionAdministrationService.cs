namespace MDRAVA.BLL.ControlPlane;

public sealed class ProxyConfigurationProjectionAdministrationService<TProjection>
    where TProjection : class
{
    private readonly IProxyConfigurationProjectionOperations<TProjection> _projectionOperations;

    public ProxyConfigurationProjectionAdministrationService(
        IProxyConfigurationProjectionOperations<TProjection> projectionOperations)
    {
        _projectionOperations = projectionOperations;
    }

    public ProxyConfigurationProjectionReadResult<TProjection> GetActive()
    {
        return _projectionOperations.GetActive();
    }

    public ProxyConfigurationProjectionReadResult<TProjection> GetEffective()
    {
        return _projectionOperations.GetEffective();
    }
}
