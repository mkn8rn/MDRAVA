using BusinessConfigLintRequest = MDRAVA.BLL.ControlPlane.ConfigLint.ConfigLintRequest;

namespace MDRAVA.API.Controllers;

public sealed record ProxyConfigLintSubmissionRequest(
    string? Format,
    string? Text)
{
    public BusinessConfigLintRequest ToConfigLintRequest()
    {
        return new BusinessConfigLintRequest(Format, Text);
    }
}
