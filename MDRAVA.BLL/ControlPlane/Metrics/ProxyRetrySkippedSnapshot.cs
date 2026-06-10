namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyRetrySkippedSnapshot(
    string Reason,
    long Count);
