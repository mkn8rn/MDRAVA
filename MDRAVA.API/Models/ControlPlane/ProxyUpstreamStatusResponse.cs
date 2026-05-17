using MDRAVA.API.Proxy.Health;

namespace MDRAVA.API.Models.ControlPlane;

public sealed record ProxyUpstreamStatusResponse(
    string RouteName,
    string UpstreamName,
    string Endpoint,
    string Scheme,
    bool TlsCertificateValidationEnabled,
    string? SniHost,
    bool HealthCheckEnabled,
    UpstreamHealthState HealthState,
    string? LastHealthCheckResult,
    DateTimeOffset? LastHealthCheckAtUtc,
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
