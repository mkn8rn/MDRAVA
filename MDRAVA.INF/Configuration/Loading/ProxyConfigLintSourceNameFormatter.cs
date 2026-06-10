using MDRAVA.BLL.ControlPlane.ConfigLint;

namespace MDRAVA.INF.Configuration.Loading;

public sealed class ProxyConfigLintSourceNameFormatter : IProxyConfigLintSourceNameFormatter
{
    public string? FormatSourceName(string? sourcePath)
    {
        return string.IsNullOrWhiteSpace(sourcePath) ? null : Path.GetFileName(sourcePath);
    }
}
