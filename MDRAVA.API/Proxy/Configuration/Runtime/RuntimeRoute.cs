namespace MDRAVA.API.Proxy.Configuration.Runtime;

public sealed record RuntimeRoute(
    string Name,
    string Host,
    string PathPrefix,
    string LoadBalancingPolicy,
    RuntimeHealthCheckOptions HealthCheck,
    IReadOnlyList<RuntimeUpstream> Upstreams);
