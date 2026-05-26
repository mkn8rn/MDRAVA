namespace MDRAVA.BLL.ControlPlane;

public interface IProxyConfigLintActiveConfigurationSource
{
    bool TryRead(out ProxyConfigLintConfigurationSnapshot? snapshot);
}
