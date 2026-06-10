namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public interface IProxyConfigLintSourceNameFormatter
{
    string? FormatSourceName(string? sourcePath);
}
