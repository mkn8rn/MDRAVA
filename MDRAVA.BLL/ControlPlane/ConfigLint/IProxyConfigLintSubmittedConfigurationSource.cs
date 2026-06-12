using MDRAVA.BLL.ControlPlane.ConfigurationManagement;
namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public interface IProxyConfigLintSubmittedConfigurationSource
{
    ProxyConfigLintSubmittedConfigurationResult Read(
        string text,
        ProxyConfigurationNormalizeFormat format,
        DateTimeOffset loadedAtUtc);
}
