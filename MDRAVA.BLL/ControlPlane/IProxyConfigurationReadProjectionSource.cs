namespace MDRAVA.BLL.ControlPlane;

public interface IProxyConfigurationReadProjectionSource<TConfiguration>
    where TConfiguration : class
{
    TConfiguration? ReadCurrent();
}
