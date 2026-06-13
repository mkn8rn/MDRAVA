using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.BLL.ControlPlane.Status;

using BusinessProxyUpstreamStatus = MDRAVA.BLL.ControlPlane.Status.ProxyUpstreamStatus;

namespace MDRAVA.API.Controllers;

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

    public CircuitBreakerStatus CircuitBreaker { get; init; } =
        CircuitBreakerStatus.Disabled(RuntimeCircuitBreakerPolicy.Disabled);

    public static IReadOnlyList<ProxyUpstreamStatusResponse> FromStatuses(
        IReadOnlyList<BusinessProxyUpstreamStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        if (statuses.Count == 0)
        {
            return [];
        }

        var responses = new List<ProxyUpstreamStatusResponse>(statuses.Count);
        foreach (var status in statuses)
        {
            responses.Add(FromStatus(status));
        }

        return responses;
    }

    public static ProxyUpstreamStatusResponse FromStatus(BusinessProxyUpstreamStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new ProxyUpstreamStatusResponse(
            status.RouteName,
            status.UpstreamName,
            status.Endpoint,
            status.Scheme,
            status.TlsCertificateValidationEnabled,
            status.SniHost,
            status.HealthCheckEnabled,
            status.HealthState,
            status.LastHealthCheckResult,
            status.LastHealthCheckAtUtc,
            status.ConsecutiveSuccesses,
            status.ConsecutiveFailures,
            status.SelectedRequests,
            status.RequestFailures)
        {
            Protocol = status.Protocol,
            Weight = status.Weight,
            CircuitBreaker = status.CircuitBreaker
        };
    }
}
