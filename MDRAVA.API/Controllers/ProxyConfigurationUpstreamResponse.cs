using BusinessRuntimeCircuitBreakerProjection = MDRAVA.BLL.Configuration.RuntimeCircuitBreakerProjection;
using BusinessRuntimeUpstreamProjection = MDRAVA.BLL.Configuration.RuntimeUpstreamProjection;
using BusinessRuntimeUpstreamTlsProjection = MDRAVA.BLL.Configuration.RuntimeUpstreamTlsProjection;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeUpstreamResponse
{
    public RuntimeUpstreamResponse(
        string routeName,
        string name,
        string scheme,
        string protocol,
        string address,
        int port,
        int weight,
        RuntimeUpstreamTlsResponse tls,
        string endpoint,
        string uriEndpoint,
        string effectiveSniHost,
        string identity,
        RuntimeCircuitBreakerResponse circuitBreaker)
    {
        ArgumentNullException.ThrowIfNull(tls);
        ArgumentNullException.ThrowIfNull(circuitBreaker);

        RouteName = routeName;
        Name = name;
        Scheme = scheme;
        Protocol = protocol;
        Address = address;
        Port = port;
        Weight = weight;
        Tls = tls;
        Endpoint = endpoint;
        UriEndpoint = uriEndpoint;
        EffectiveSniHost = effectiveSniHost;
        Identity = identity;
        CircuitBreaker = circuitBreaker;
    }

    public string RouteName { get; }

    public string Name { get; }

    public string Scheme { get; }

    public string Protocol { get; }

    public string Address { get; }

    public int Port { get; }

    public int Weight { get; }

    public RuntimeUpstreamTlsResponse Tls { get; }

    public string Endpoint { get; }

    public string UriEndpoint { get; }

    public string EffectiveSniHost { get; }

    public string Identity { get; }

    public RuntimeCircuitBreakerResponse CircuitBreaker { get; }

    public static IReadOnlyList<RuntimeUpstreamResponse> FromUpstreams(
        IReadOnlyList<BusinessRuntimeUpstreamProjection> upstreams)
    {
        ArgumentNullException.ThrowIfNull(upstreams);

        return ApiResponseList.Copy(upstreams.Select(FromUpstream));
    }

    private static RuntimeUpstreamResponse FromUpstream(BusinessRuntimeUpstreamProjection upstream)
    {
        ArgumentNullException.ThrowIfNull(upstream);

        return new RuntimeUpstreamResponse(
            routeName: upstream.RouteName,
            name: upstream.Name,
            scheme: upstream.Scheme,
            protocol: upstream.Protocol,
            address: upstream.Address,
            port: upstream.Port,
            weight: upstream.Weight,
            tls: RuntimeUpstreamTlsResponse.FromProjection(upstream.Tls),
            endpoint: upstream.Endpoint,
            uriEndpoint: upstream.UriEndpoint,
            effectiveSniHost: upstream.EffectiveSniHost,
            identity: upstream.Identity,
            circuitBreaker: RuntimeCircuitBreakerResponse.FromProjection(upstream.CircuitBreaker));
    }
}

public sealed record RuntimeUpstreamTlsResponse(
    bool ValidateCertificate,
    string? SniHost)
{
    public static RuntimeUpstreamTlsResponse FromProjection(BusinessRuntimeUpstreamTlsProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeUpstreamTlsResponse(projection.ValidateCertificate, projection.SniHost);
    }
}

public sealed record RuntimeCircuitBreakerResponse
{
    public RuntimeCircuitBreakerResponse(
        bool enabled,
        int failureThreshold,
        TimeSpan samplingWindow,
        TimeSpan openDuration,
        int halfOpenMaxAttempts,
        IReadOnlyList<int> failureStatusCodes)
    {
        Enabled = enabled;
        FailureThreshold = failureThreshold;
        SamplingWindow = samplingWindow;
        OpenDuration = openDuration;
        HalfOpenMaxAttempts = halfOpenMaxAttempts;
        FailureStatusCodes = ApiResponseList.Copy(failureStatusCodes);
    }

    public bool Enabled { get; }

    public int FailureThreshold { get; }

    public TimeSpan SamplingWindow { get; }

    public TimeSpan OpenDuration { get; }

    public int HalfOpenMaxAttempts { get; }

    public IReadOnlyList<int> FailureStatusCodes { get; }

    public static RuntimeCircuitBreakerResponse FromProjection(BusinessRuntimeCircuitBreakerProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeCircuitBreakerResponse(
            enabled: projection.Enabled,
            failureThreshold: projection.FailureThreshold,
            samplingWindow: projection.SamplingWindow,
            openDuration: projection.OpenDuration,
            halfOpenMaxAttempts: projection.HalfOpenMaxAttempts,
            failureStatusCodes: projection.FailureStatusCodes);
    }
}
