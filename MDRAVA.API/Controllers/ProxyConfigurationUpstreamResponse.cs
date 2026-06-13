using BusinessRuntimeCircuitBreakerPolicy = MDRAVA.BLL.Configuration.RuntimeCircuitBreakerPolicy;
using BusinessRuntimeUpstream = MDRAVA.BLL.Configuration.RuntimeUpstream;
using BusinessRuntimeUpstreamTlsOptions = MDRAVA.BLL.Configuration.RuntimeUpstreamTlsOptions;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeUpstreamResponse(
    string RouteName,
    string Name,
    string Scheme,
    string Protocol,
    string Address,
    int Port,
    int Weight,
    RuntimeUpstreamTlsResponse Tls)
{
    public string Endpoint { get; init; } = "";

    public string UriEndpoint { get; init; } = "";

    public string EffectiveSniHost { get; init; } = "";

    public string Identity { get; init; } = "";

    public RuntimeCircuitBreakerResponse CircuitBreaker { get; init; } = null!;

    public static IReadOnlyList<RuntimeUpstreamResponse> FromUpstreams(
        IReadOnlyList<BusinessRuntimeUpstream> upstreams)
    {
        ArgumentNullException.ThrowIfNull(upstreams);

        return upstreams.Select(FromUpstream).ToArray();
    }

    private static RuntimeUpstreamResponse FromUpstream(BusinessRuntimeUpstream upstream)
    {
        ArgumentNullException.ThrowIfNull(upstream);

        return new RuntimeUpstreamResponse(
            upstream.RouteName,
            upstream.Name,
            upstream.Scheme,
            upstream.Protocol,
            upstream.Address,
            upstream.Port,
            upstream.Weight,
            RuntimeUpstreamTlsResponse.FromOptions(upstream.Tls))
        {
            Endpoint = upstream.Endpoint,
            UriEndpoint = upstream.UriEndpoint,
            EffectiveSniHost = upstream.EffectiveSniHost,
            Identity = upstream.Identity,
            CircuitBreaker = RuntimeCircuitBreakerResponse.FromPolicy(upstream.CircuitBreaker)
        };
    }
}

public sealed record RuntimeUpstreamTlsResponse(
    bool ValidateCertificate,
    string? SniHost)
{
    public static RuntimeUpstreamTlsResponse FromOptions(BusinessRuntimeUpstreamTlsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RuntimeUpstreamTlsResponse(options.ValidateCertificate, options.SniHost);
    }
}

public sealed record RuntimeCircuitBreakerResponse(
    bool Enabled,
    int FailureThreshold,
    TimeSpan SamplingWindow,
    TimeSpan OpenDuration,
    int HalfOpenMaxAttempts,
    IReadOnlyList<int> FailureStatusCodes)
{
    public static RuntimeCircuitBreakerResponse FromPolicy(BusinessRuntimeCircuitBreakerPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new RuntimeCircuitBreakerResponse(
            policy.Enabled,
            policy.FailureThreshold,
            policy.SamplingWindow,
            policy.OpenDuration,
            policy.HalfOpenMaxAttempts,
            policy.FailureStatusCodes.ToArray());
    }
}
