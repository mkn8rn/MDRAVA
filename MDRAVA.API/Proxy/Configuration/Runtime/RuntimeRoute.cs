namespace MDRAVA.API.Proxy.Configuration.Runtime;

public sealed record RuntimeRoute(
    string Name,
    string Host,
    string PathPrefix,
    IReadOnlyList<RuntimeUpstream> Upstreams);
