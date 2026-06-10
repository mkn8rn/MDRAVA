using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Resilience;

public interface IProxyCircuitBreakerMetricsSink
{
    void CircuitOpened(RuntimeUpstream upstream);

    void CircuitHalfOpened(RuntimeUpstream upstream);

    void CircuitClosed(RuntimeUpstream upstream);

    void CircuitRejected(RuntimeUpstream upstream);
}
