namespace MDRAVA.BLL.ControlPlane;

public sealed class ProxyConfigurationReadOperations<TConfiguration>
    : IProxyConfigurationReadOperations<TConfiguration>
    where TConfiguration : class
{
    private readonly IProxyConfigurationReadProjectionSource<TConfiguration> _projectionSource;

    public ProxyConfigurationReadOperations(
        IProxyConfigurationReadProjectionSource<TConfiguration> projectionSource)
    {
        _projectionSource = projectionSource;
    }

    public ProxyConfigurationReadResult<TConfiguration> ReadActive()
    {
        return ReadCurrent();
    }

    public ProxyConfigurationReadResult<TConfiguration> ReadEffective()
    {
        return ReadCurrent();
    }

    private ProxyConfigurationReadResult<TConfiguration> ReadCurrent()
    {
        var projection = _projectionSource.ReadCurrent();
        return new ProxyConfigurationReadResult<TConfiguration>(
            projection is not null,
            projection);
    }
}
