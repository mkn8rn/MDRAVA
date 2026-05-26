namespace MDRAVA.BLL.ControlPlane;

public interface IProxyConfigLintMetricsSink
{
    void ConfigLintRun(IReadOnlyList<ConfigLintFinding> findings);
}
