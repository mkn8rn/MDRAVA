namespace MDRAVA.API.Models.ControlPlane;

public sealed record ProxyLogPersistenceFailureStatus(
    DateTimeOffset TimestampUtc,
    string Category,
    string Reason);
