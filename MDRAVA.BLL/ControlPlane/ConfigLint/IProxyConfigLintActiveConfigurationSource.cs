namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public interface IProxyConfigLintActiveConfigurationSource
{
    bool TryRead(out ProxyConfigLintConfigurationSnapshot? snapshot);
}
