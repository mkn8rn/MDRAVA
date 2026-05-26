using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane;

public interface IProxyConfigurationNormalizeSiteParser
{
    ProxyConfigurationNormalizeSiteParseResult Parse(
        string text,
        ProxyConfigurationNormalizeFormat format);
}
