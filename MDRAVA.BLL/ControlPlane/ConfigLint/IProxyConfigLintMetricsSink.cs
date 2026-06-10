namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public interface IProxyConfigLintMetricsSink
{
    void ConfigLintRun(IReadOnlyList<ConfigLintFinding> findings);
}
