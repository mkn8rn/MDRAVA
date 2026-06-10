using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public interface IProxyConfigurationNormalizeSiteParser
{
    ProxyConfigurationNormalizeSiteParseResult Parse(
        string text,
        ProxyConfigurationNormalizeFormat format);
}
