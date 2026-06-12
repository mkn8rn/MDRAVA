using MDRAVA.BLL.ControlPlane.ConfigurationManagement;

namespace MDRAVA.API.Controllers;

public sealed record ProxyConfigurationNormalizeSubmissionRequest(
    string Format,
    string Text)
{
    public ProxyConfigurationNormalizeRequest ToNormalizeRequest()
    {
        return new ProxyConfigurationNormalizeRequest(Format, Text);
    }
}
