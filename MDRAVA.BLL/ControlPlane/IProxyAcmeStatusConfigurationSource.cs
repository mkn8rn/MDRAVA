namespace MDRAVA.BLL.ControlPlane;

public interface IProxyAcmeStatusConfigurationSource
{
    bool TryGetSnapshot(out ProxyAcmeStatusConfigurationSourceSnapshot? snapshot);
}
