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
    long RequestFailures)
{
    public string Protocol { get; init; } = RuntimeUpstreamProtocol.Http1;

    public int Weight { get; init; } = 1;

    public CircuitBreakerStatus CircuitBreaker { get; init; } = new(
        CircuitBreakerRuntimeState.Disabled,
        false,
        5,
        1,
        null,
        null,
        0,
        0,
        null);
}
