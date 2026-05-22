namespace MDRAVA.BLL.ControlPlane;

public sealed record ProxyLogPersistenceFailureStatus(
    DateTimeOffset TimestampUtc,
    string Category,
    string Reason);
