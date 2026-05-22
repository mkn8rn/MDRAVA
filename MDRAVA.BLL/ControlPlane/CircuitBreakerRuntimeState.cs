namespace MDRAVA.BLL.ControlPlane;

public enum CircuitBreakerRuntimeState
{
    Disabled = 0,
    Closed,
    Open,
    HalfOpen
}
