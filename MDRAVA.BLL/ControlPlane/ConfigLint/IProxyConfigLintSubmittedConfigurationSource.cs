using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public interface IProxyConfigLintSubmittedConfigurationSource
{
    ProxyConfigLintSubmittedConfigurationResult Read(
        ConfigLintRequest request,
        ProxyConfigurationNormalizeFormat format,
        DateTimeOffset loadedAtUtc);
}
