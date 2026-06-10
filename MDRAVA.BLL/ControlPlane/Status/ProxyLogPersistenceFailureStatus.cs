using MDRAVA.BLL.ControlPlane;
namespace MDRAVA.BLL.ControlPlane.Status;

public sealed record ProxyLogPersistenceFailureStatus(
    DateTimeOffset TimestampUtc,
    string Category,
    string Reason);
