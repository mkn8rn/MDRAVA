namespace MDRAVA.BLL.ControlPlane.Resilience;

public interface IProxyCircuitBreakerMetricsSink
{
    void CircuitOpened();

    void CircuitHalfOpened();

    void CircuitClosed();

    void CircuitRejected();
}
