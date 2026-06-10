namespace MDRAVA.BLL.ControlPlane.Acme;

public interface IProxyAcmeStatusConfigurationSource
{
    bool TryGetSnapshot(out ProxyAcmeStatusConfigurationSourceSnapshot? snapshot);
}
