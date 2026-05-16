namespace MDRAVA.API.Models.Health;

public sealed record UpstreamHealthRecord(
    string RouteName,
    string UpstreamName,
    string Endpoint,
    string Scheme,
    bool TlsCertificateValidationEnabled,
    string? SniHost,
    bool HealthCheckEnabled,
    UpstreamHealthState State,
    string? LastResult,
    DateTimeOffset? LastCheckedAtUtc,
    int ConsecutiveSuccesses,
    int ConsecutiveFailures,
    long SelectedRequests,
    long RequestFailures);
