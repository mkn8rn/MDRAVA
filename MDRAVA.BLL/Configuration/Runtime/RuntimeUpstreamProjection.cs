namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeUpstreamProjection
{
    public RuntimeUpstreamProjection(
        string RouteName,
        string Name,
        string Scheme,
        string Protocol,
        string Address,
        int Port,
        int Weight,
        RuntimeUpstreamTlsProjection Tls,
        string Endpoint,
        string UriEndpoint,
        string EffectiveSniHost,
        string Identity,
        RuntimeCircuitBreakerProjection CircuitBreaker)
    {
        this.RouteName = RouteName;
        this.Name = Name;
        this.Scheme = Scheme;
        this.Protocol = Protocol;
        this.Address = Address;
        this.Port = Port;
        this.Weight = Weight;
        this.Tls = Tls;
        this.Endpoint = Endpoint;
        this.UriEndpoint = UriEndpoint;
        this.EffectiveSniHost = EffectiveSniHost;
        this.Identity = Identity;
        this.CircuitBreaker = CircuitBreaker;
    }

    public string RouteName { get; init; }

    public string Name { get; init; }

    public string Scheme { get; init; }

    public string Protocol { get; init; }

    public string Address { get; init; }

    public int Port { get; init; }

    public int Weight { get; init; }

    public RuntimeUpstreamTlsProjection Tls { get; init; }

    public string Endpoint { get; init; }

    public string UriEndpoint { get; init; }

    public string EffectiveSniHost { get; init; }

    public string Identity { get; init; }

    public RuntimeCircuitBreakerProjection CircuitBreaker { get; init; }
}
