using BusinessCircuitBreakerRuntimeState = MDRAVA.BLL.ControlPlane.Resilience.CircuitBreakerRuntimeState;
using BusinessCircuitBreakerStatus = MDRAVA.BLL.ControlPlane.Resilience.CircuitBreakerStatus;
using BusinessUpstreamHealthState = MDRAVA.BLL.ControlPlane.HealthChecks.UpstreamHealthState;

namespace MDRAVA.API.Controllers;

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
