namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyConfigLintFindingMetricSnapshot(
    string Severity,
    string Code,
    long Count);
