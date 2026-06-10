namespace MDRAVA.BLL.ControlPlane.Caching;

public interface IProxyCacheStatusReader
{
    ProxyCacheStatusResponse GetStatus();
}
