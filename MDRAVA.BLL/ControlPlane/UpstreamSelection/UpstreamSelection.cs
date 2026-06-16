using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.UpstreamSelection;

public sealed record UpstreamSelectionRoute
{
    public UpstreamSelectionRoute(
        string Name,
        bool HealthCheckEnabled,
        IReadOnlyList<RuntimeUpstream> Upstreams)
    {
        this.Name = Name;
        this.HealthCheckEnabled = HealthCheckEnabled;
        this.Upstreams = RuntimeList.Copy(Upstreams);
    }

    public string Name { get; init; }

    public bool HealthCheckEnabled { get; init; }

    public IReadOnlyList<RuntimeUpstream> Upstreams { get; }
}

public sealed record UpstreamSelection(
    RuntimeUpstream Upstream,
    CircuitBreakerLease CircuitBreakerLease);
