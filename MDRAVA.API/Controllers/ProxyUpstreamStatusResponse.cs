using BusinessCircuitBreakerRuntimeState = MDRAVA.BLL.ControlPlane.Resilience.CircuitBreakerRuntimeState;
using BusinessCircuitBreakerStatus = MDRAVA.BLL.ControlPlane.Resilience.CircuitBreakerStatus;
using BusinessProxyUpstreamStatus = MDRAVA.BLL.ControlPlane.Status.ProxyUpstreamStatus;
using BusinessUpstreamHealthState = MDRAVA.BLL.ControlPlane.HealthChecks.UpstreamHealthState;

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

public enum UpstreamHealthStateResponse
{
    Unknown = 0,
    Healthy = 1,
    Unhealthy = 2
}

public static class UpstreamHealthStateResponseMapper
{
    public static UpstreamHealthStateResponse FromState(BusinessUpstreamHealthState state)
    {
        return state switch
        {
            BusinessUpstreamHealthState.Unknown => UpstreamHealthStateResponse.Unknown,
            BusinessUpstreamHealthState.Healthy => UpstreamHealthStateResponse.Healthy,
            BusinessUpstreamHealthState.Unhealthy => UpstreamHealthStateResponse.Unhealthy,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }
}

public sealed record CircuitBreakerStatusResponse(
    CircuitBreakerRuntimeStateResponse State,
    bool Enabled,
    int FailureThreshold,
    int HalfOpenMaxAttempts,
    DateTimeOffset? OpenedAtUtc,
    DateTimeOffset? NextAttemptAtUtc,
    int FailureCount,
    long RejectedRequests,
    string? LastFailureReason)
{
    public static CircuitBreakerStatusResponse Disabled { get; } = new(
        CircuitBreakerRuntimeStateResponse.Disabled,
        Enabled: false,
        FailureThreshold: 5,
        HalfOpenMaxAttempts: 1,
        OpenedAtUtc: null,
        NextAttemptAtUtc: null,
        FailureCount: 0,
        RejectedRequests: 0,
        LastFailureReason: null);

    public static CircuitBreakerStatusResponse FromStatus(BusinessCircuitBreakerStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        return new CircuitBreakerStatusResponse(
            CircuitBreakerRuntimeStateResponseMapper.FromState(status.State),
            status.Enabled,
            status.FailureThreshold,
            status.HalfOpenMaxAttempts,
            status.OpenedAtUtc,
            status.NextAttemptAtUtc,
            status.FailureCount,
            status.RejectedRequests,
            status.LastFailureReason);
    }
}

public enum CircuitBreakerRuntimeStateResponse
{
    Disabled = 0,
    Closed = 1,
    Open = 2,
    HalfOpen = 3
}

public static class CircuitBreakerRuntimeStateResponseMapper
{
    public static CircuitBreakerRuntimeStateResponse FromState(BusinessCircuitBreakerRuntimeState state)
    {
        return state switch
        {
            BusinessCircuitBreakerRuntimeState.Disabled => CircuitBreakerRuntimeStateResponse.Disabled,
            BusinessCircuitBreakerRuntimeState.Closed => CircuitBreakerRuntimeStateResponse.Closed,
            BusinessCircuitBreakerRuntimeState.Open => CircuitBreakerRuntimeStateResponse.Open,
            BusinessCircuitBreakerRuntimeState.HalfOpen => CircuitBreakerRuntimeStateResponse.HalfOpen,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }
}
