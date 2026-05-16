using MDRAVA.API.Proxy.Health;

namespace MDRAVA.API.Controllers;

public sealed record ProxyUpstreamStatusResponse(
    string RouteName,
    string UpstreamName,
    string Endpoint,
    bool HealthCheckEnabled,
    UpstreamHealthState HealthState,
    string? LastHealthCheckResult,
    DateTimeOffset? LastHealthCheckAtUtc,
    int ConsecutiveSuccesses,
    int ConsecutiveFailures,
    long SelectedRequests,
    long RequestFailures);
