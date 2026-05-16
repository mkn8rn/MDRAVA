namespace MDRAVA.API.Proxy.Health;

public sealed record UpstreamHealthRecord(
    string RouteName,
    string UpstreamName,
    string Endpoint,
    bool HealthCheckEnabled,
    UpstreamHealthState State,
    string? LastResult,
    DateTimeOffset? LastCheckedAtUtc,
    int ConsecutiveSuccesses,
    int ConsecutiveFailures,
    long SelectedRequests,
    long RequestFailures);
