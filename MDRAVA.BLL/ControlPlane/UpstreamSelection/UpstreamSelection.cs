using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.UpstreamSelection;

public sealed record UpstreamSelectionRoute(
    string Name,
    bool HealthCheckEnabled,
    IReadOnlyList<RuntimeUpstream> Upstreams)
{
    public static UpstreamSelectionRoute FromRuntime(RuntimeRoute route)
    {
        return new UpstreamSelectionRoute(
            route.Name,
            route.HealthCheck.Enabled,
            route.Upstreams);
    }
}

public sealed record UpstreamSelection(
    RuntimeUpstream Upstream,
    CircuitBreakerLease CircuitBreakerLease);
