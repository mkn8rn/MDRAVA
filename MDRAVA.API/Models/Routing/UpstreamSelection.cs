using MDRAVA.API.Proxy.Resilience;

namespace MDRAVA.API.Models.Routing;

public sealed record UpstreamSelection(
    RuntimeRoute Route,
    RuntimeUpstream Upstream,
    CircuitBreakerLease? CircuitBreakerLease = null);
