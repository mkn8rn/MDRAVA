using BusinessProxyConfigurationNormalizeRequest =
    MDRAVA.BLL.ControlPlane.ConfigurationManagement.ProxyConfigurationNormalizeRequest;

namespace MDRAVA.API.Controllers;

public sealed record ProxyConfigurationNormalizeSubmissionRequest(
    string? Format,
    string? Text)
{
    public BusinessProxyConfigurationNormalizeRequest ToNormalizeRequest()
    {
        return new BusinessProxyConfigurationNormalizeRequest(Format, Text);
    }
}
