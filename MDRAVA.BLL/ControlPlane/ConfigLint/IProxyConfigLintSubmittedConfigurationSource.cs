namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public interface IProxyConfigLintSubmittedConfigurationSource
{
    ProxyConfigLintSubmittedConfigurationResult Read(
        ConfigLintRequest request,
        ProxyConfigurationNormalizeFormat format,
        DateTimeOffset loadedAtUtc);
}
