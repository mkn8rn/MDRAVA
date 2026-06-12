namespace MDRAVA.BLL.ControlPlane.Resilience;

using MDRAVA.BLL.Configuration;

public sealed record CircuitBreakerStatus(
    CircuitBreakerRuntimeState State,
    bool Enabled,
    int FailureThreshold,
    int HalfOpenMaxAttempts,
    DateTimeOffset? OpenedAtUtc,
    DateTimeOffset? NextAttemptAtUtc,
    int FailureCount,
    long RejectedRequests,
    string? LastFailureReason)
{
    public static CircuitBreakerStatus Disabled(RuntimeCircuitBreakerPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new CircuitBreakerStatus(
            CircuitBreakerRuntimeState.Disabled,
            false,
            policy.FailureThreshold,
            policy.HalfOpenMaxAttempts,
            null,
            null,
            0,
            0,
            null);
    }

    public static CircuitBreakerStatus FromEnabledPolicyState(
        RuntimeCircuitBreakerPolicy policy,
        CircuitBreakerRuntimeState state,
        DateTimeOffset? openedAtUtc,
        int failureCount,
        long rejectedRequests,
        string? lastFailureReason)
    {
        ArgumentNullException.ThrowIfNull(policy);
        if (!policy.Enabled)
        {
            throw new ArgumentException("Circuit breaker policy must be enabled.", nameof(policy));
        }

        DateTimeOffset? nextAttemptAtUtc = state == CircuitBreakerRuntimeState.Open && openedAtUtc.HasValue
            ? openedAtUtc.Value.Add(policy.OpenDuration)
            : null;
        return new CircuitBreakerStatus(
            state,
            true,
            policy.FailureThreshold,
            policy.HalfOpenMaxAttempts,
            openedAtUtc,
            nextAttemptAtUtc,
            failureCount,
            rejectedRequests,
            lastFailureReason);
    }
}
