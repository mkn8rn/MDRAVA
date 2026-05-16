namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeMaintenancePolicy(
    bool Enabled,
    int? RetryAfterSeconds,
    string ContentType,
    string Body);
