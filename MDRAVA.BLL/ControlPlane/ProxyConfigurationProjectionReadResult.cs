namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyConfigurationProjectionReadResult<TProjection>(
    bool Found,
    TProjection? Projection)
    where TProjection : class;
