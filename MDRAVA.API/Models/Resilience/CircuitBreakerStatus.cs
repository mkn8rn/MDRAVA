namespace MDRAVA.API.Models.Resilience;

public sealed record CircuitBreakerStatus(
    CircuitBreakerRuntimeState State,
    bool Enabled,
    int FailureThreshold,
    int HalfOpenMaxAttempts,
    DateTimeOffset? OpenedAtUtc,
    DateTimeOffset? NextAttemptAtUtc,
    int FailureCount,
    long RejectedRequests,
    string? LastFailureReason);
