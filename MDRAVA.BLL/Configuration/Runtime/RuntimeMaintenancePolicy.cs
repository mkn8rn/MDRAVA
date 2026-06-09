namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeMaintenancePolicy(
    bool Enabled,
    int? RetryAfterSeconds,
    string ContentType,
    string Body);
