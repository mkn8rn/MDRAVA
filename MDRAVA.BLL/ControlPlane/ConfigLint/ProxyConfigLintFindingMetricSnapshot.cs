namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public sealed record ProxyConfigLintFindingMetricSnapshot(
    string Severity,
    string Code,
    long Count);
