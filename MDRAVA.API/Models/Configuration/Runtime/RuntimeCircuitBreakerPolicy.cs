namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeCircuitBreakerPolicy(
    bool Enabled,
    int FailureThreshold,
    TimeSpan SamplingWindow,
    TimeSpan OpenDuration,
    int HalfOpenMaxAttempts,
    IReadOnlyList<int> FailureStatusCodes)
{
    public static RuntimeCircuitBreakerPolicy Disabled { get; } = new(
        false,
        5,
        TimeSpan.FromSeconds(60),
        TimeSpan.FromSeconds(30),
        1,
        []);
}
