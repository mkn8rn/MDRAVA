namespace MDRAVA.BLL.ControlPlane;

public interface IProxyConfigLintSubmittedConfigurationSource
{
    ProxyConfigLintSubmittedConfigurationResult Read(
        ConfigLintRequest request,
        ProxyConfigurationNormalizeFormat format,
        DateTimeOffset loadedAtUtc);
}
