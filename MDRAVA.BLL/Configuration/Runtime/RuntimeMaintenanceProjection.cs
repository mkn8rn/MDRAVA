namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeMaintenanceProjection(
    bool Enabled,
    int? RetryAfterSeconds,
    string ContentType,
    string Body);
