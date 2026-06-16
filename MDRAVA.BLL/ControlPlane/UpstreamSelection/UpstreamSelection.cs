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
        ArgumentNullException.ThrowIfNull(Name);

        this.Name = Name;
        this.HealthCheckEnabled = HealthCheckEnabled;
        this.Upstreams = RuntimeList.Copy(Upstreams);
    }

    public string Name { get; }

    public bool HealthCheckEnabled { get; }

    public IReadOnlyList<RuntimeUpstream> Upstreams { get; }
}

public sealed record UpstreamSelection(
    RuntimeUpstream Upstream,
    CircuitBreakerLease CircuitBreakerLease);
