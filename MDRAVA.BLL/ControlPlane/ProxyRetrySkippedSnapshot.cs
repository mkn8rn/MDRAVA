namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyRetrySkippedSnapshot(
    string Reason,
    long Count);
