using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.UpstreamSelection;

public sealed record UpstreamSelection(
    RuntimeRoute Route,
    RuntimeUpstream Upstream,
    CircuitBreakerLease? CircuitBreakerLease = null);
