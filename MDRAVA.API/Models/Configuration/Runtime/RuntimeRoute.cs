namespace MDRAVA.API.Models.Configuration.Runtime;

public sealed record RuntimeRoute(
    string Name,
    string Host,
    string PathPrefix,
    string LoadBalancingPolicy,
    RuntimeHealthCheckOptions HealthCheck,
    IReadOnlyList<RuntimeUpstream> Upstreams);
