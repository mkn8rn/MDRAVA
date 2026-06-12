using MDRAVA.BLL.ControlPlane.ConfigLint;

namespace MDRAVA.API.Controllers;

public sealed record ProxyConfigLintSubmissionRequest(
    string Format,
    string Text)
{
    public ConfigLintRequest ToConfigLintRequest()
    {
        return new ConfigLintRequest(Format, Text);
    }
}
