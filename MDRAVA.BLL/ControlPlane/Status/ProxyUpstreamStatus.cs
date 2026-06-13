using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.BLL.ControlPlane.HealthChecks;

namespace MDRAVA.BLL.ControlPlane.Status;

public sealed record ProxyUpstreamStatus(
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

    public CircuitBreakerStatus CircuitBreaker { get; init; } =
        CircuitBreakerStatus.Disabled(RuntimeCircuitBreakerPolicy.Disabled);
}
