namespace MDRAVA.API.Models.Resilience;

public enum CircuitBreakerRuntimeState
{
    Disabled = 0,
    Closed,
    Open,
    HalfOpen
}
