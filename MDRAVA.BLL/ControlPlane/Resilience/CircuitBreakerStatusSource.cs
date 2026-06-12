using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Resilience;

public sealed record CircuitBreakerStatusSource(
    string UpstreamIdentity,
    RuntimeCircuitBreakerPolicy Policy);

public static class CircuitBreakerStatusSourceMapper
{
    public static CircuitBreakerStatusSource FromUpstream(RuntimeUpstream upstream)
    {
        return new CircuitBreakerStatusSource(
            upstream.Identity,
            upstream.CircuitBreaker);
    }
}
