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
    UpstreamHealthStateResponse HealthState,
    string? LastHealthCheckResult,
    DateTimeOffset? LastHealthCheckAtUtc,
    int ConsecutiveSuccesses,
    int ConsecutiveFailures,
    long SelectedRequests,
    long RequestFailures)
{
    public string Protocol { get; init; } = "http1";

    public int Weight { get; init; } = 1;

    public CircuitBreakerStatusResponse CircuitBreaker { get; init; } =
        CircuitBreakerStatusResponse.Disabled;

    public static IReadOnlyList<ProxyUpstreamStatusResponse> FromStatuses(
        IEnumerable<BusinessProxyUpstreamStatus> statuses)
    {
        ArgumentNullException.ThrowIfNull(statuses);

        return ApiResponseList.Copy(statuses.Select(FromStatus));
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
            UpstreamHealthStateResponseMapper.FromState(status.HealthState),
            status.LastHealthCheckResult,
            status.LastHealthCheckAtUtc,
            status.ConsecutiveSuccesses,
            status.ConsecutiveFailures,
            status.SelectedRequests,
            status.RequestFailures)
        {
            Protocol = status.Protocol,
            Weight = status.Weight,
            CircuitBreaker = CircuitBreakerStatusResponse.FromStatus(status.CircuitBreaker)
        };
    }
}
