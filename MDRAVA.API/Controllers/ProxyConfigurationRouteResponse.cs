using BusinessProxyHeaderField = MDRAVA.BLL.Http.ProxyHeaderField;
using BusinessRuntimeCachePolicy = MDRAVA.BLL.Configuration.RuntimeCachePolicy;
using BusinessRuntimeCanonicalHostPolicy = MDRAVA.BLL.Configuration.RuntimeCanonicalHostPolicy;
using BusinessRuntimeCircuitBreakerPolicy = MDRAVA.BLL.Configuration.RuntimeCircuitBreakerPolicy;
using BusinessRuntimeHeaderPolicy = MDRAVA.BLL.Configuration.RuntimeHeaderPolicy;
using BusinessRuntimeHealthCheckOptions = MDRAVA.BLL.Configuration.RuntimeHealthCheckOptions;
using BusinessRuntimeHttpsRedirectPolicy = MDRAVA.BLL.Configuration.RuntimeHttpsRedirectPolicy;
using BusinessRuntimeMaintenancePolicy = MDRAVA.BLL.Configuration.RuntimeMaintenancePolicy;
using BusinessRuntimePathRewritePolicy = MDRAVA.BLL.Configuration.RuntimePathRewritePolicy;
using BusinessRuntimeRedirectPolicy = MDRAVA.BLL.Configuration.RuntimeRedirectPolicy;
using BusinessRuntimeRetryPolicy = MDRAVA.BLL.Configuration.RuntimeRetryPolicy;
using BusinessRuntimeRoute = MDRAVA.BLL.Configuration.RuntimeRoute;
using BusinessRuntimeRouteAction = MDRAVA.BLL.Configuration.RuntimeRouteAction;
using BusinessRuntimeRouteResolvedOptions = MDRAVA.BLL.Configuration.RuntimeRouteResolvedOptions;
using BusinessRuntimeStaticResponse = MDRAVA.BLL.Configuration.RuntimeStaticResponse;
using BusinessRuntimeUpstream = MDRAVA.BLL.Configuration.RuntimeUpstream;
using BusinessRuntimeUpstreamTlsOptions = MDRAVA.BLL.Configuration.RuntimeUpstreamTlsOptions;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeRouteResponse(
    string Name,
    string Host,
    string PathPrefix,
    RuntimeRouteActionResponse Action,
    string LoadBalancingPolicy,
    RuntimeHealthCheckResponse HealthCheck,
    IReadOnlyList<RuntimeUpstreamResponse> Upstreams,
    RuntimeHttpsRedirectResponse HttpsRedirect,
    RuntimeCanonicalHostResponse CanonicalHost,
    RuntimeHeaderPolicyResponse HeaderPolicy,
    RuntimePathRewriteResponse PathRewrite,
    RuntimeRedirectResponse Redirect,
    RuntimeStaticResponseResponse StaticResponse,
    RuntimeMaintenanceResponse Maintenance,
    RuntimeCachePolicyResponse Cache,
    RuntimeRouteResolvedOptionsResponse ResolvedOptions)
{
    public string SiteName { get; init; } = "";

    public RuntimeRetryPolicyResponse Retry { get; init; } = null!;

    public static IReadOnlyList<RuntimeRouteResponse> FromRoutes(IReadOnlyList<BusinessRuntimeRoute> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        return routes.Select(FromRoute).ToArray();
    }

    private static RuntimeRouteResponse FromRoute(BusinessRuntimeRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return new RuntimeRouteResponse(
            route.Name,
            route.Host,
            route.PathPrefix,
            RuntimeRouteActionResponseMapper.FromAction(route.Action),
            route.LoadBalancingPolicy,
            RuntimeHealthCheckResponse.FromOptions(route.HealthCheck),
            RuntimeUpstreamResponse.FromUpstreams(route.Upstreams),
            RuntimeHttpsRedirectResponse.FromPolicy(route.HttpsRedirect),
            RuntimeCanonicalHostResponse.FromPolicy(route.CanonicalHost),
            RuntimeHeaderPolicyResponse.FromPolicy(route.HeaderPolicy),
            RuntimePathRewriteResponse.FromPolicy(route.PathRewrite),
            RuntimeRedirectResponse.FromPolicy(route.Redirect),
            RuntimeStaticResponseResponse.FromResponse(route.StaticResponse),
            RuntimeMaintenanceResponse.FromPolicy(route.Maintenance),
            RuntimeCachePolicyResponse.FromPolicy(route.Cache),
            RuntimeRouteResolvedOptionsResponse.FromOptions(route.ResolvedOptions))
        {
            SiteName = route.SiteName,
            Retry = RuntimeRetryPolicyResponse.FromPolicy(route.Retry)
        };
    }
}

public sealed record RuntimeHealthCheckResponse(
    bool Enabled,
    string Path,
    TimeSpan Interval,
    TimeSpan Timeout,
    int HealthyThreshold,
    int UnhealthyThreshold)
{
    public static RuntimeHealthCheckResponse FromOptions(BusinessRuntimeHealthCheckOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RuntimeHealthCheckResponse(
            options.Enabled,
            options.Path,
            options.Interval,
            options.Timeout,
            options.HealthyThreshold,
            options.UnhealthyThreshold);
    }
}

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

public sealed record RuntimeHttpsRedirectResponse(
    bool Enabled,
    int StatusCode,
    int? HttpsPort)
{
    public static RuntimeHttpsRedirectResponse FromPolicy(BusinessRuntimeHttpsRedirectPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new RuntimeHttpsRedirectResponse(policy.Enabled, policy.StatusCode, policy.HttpsPort);
    }
}

public sealed record RuntimeCanonicalHostResponse(
    bool Enabled,
    string TargetHost,
    int StatusCode)
{
    public static RuntimeCanonicalHostResponse FromPolicy(BusinessRuntimeCanonicalHostPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new RuntimeCanonicalHostResponse(policy.Enabled, policy.TargetHost, policy.StatusCode);
    }
}

public sealed record RuntimeHeaderPolicyResponse(
    IReadOnlyList<RuntimeHeaderFieldResponse> SetRequestHeaders,
    IReadOnlyList<string> RemoveRequestHeaders,
    IReadOnlyList<RuntimeHeaderFieldResponse> SetResponseHeaders,
    IReadOnlyList<string> RemoveResponseHeaders)
{
    public static RuntimeHeaderPolicyResponse FromPolicy(BusinessRuntimeHeaderPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new RuntimeHeaderPolicyResponse(
            RuntimeHeaderFieldResponse.FromFields(policy.SetRequestHeaders),
            policy.RemoveRequestHeaders.ToArray(),
            RuntimeHeaderFieldResponse.FromFields(policy.SetResponseHeaders),
            policy.RemoveResponseHeaders.ToArray());
    }
}

public sealed record RuntimeHeaderFieldResponse(string Name, string Value)
{
    public static IReadOnlyList<RuntimeHeaderFieldResponse> FromFields(
        IReadOnlyList<BusinessProxyHeaderField> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        return fields.Select(FromField).ToArray();
    }

    private static RuntimeHeaderFieldResponse FromField(BusinessProxyHeaderField field)
    {
        ArgumentNullException.ThrowIfNull(field);

        return new RuntimeHeaderFieldResponse(field.Name, field.Value);
    }
}

public sealed record RuntimePathRewriteResponse(
    string StripPrefix,
    string ReplacePrefix,
    string Replacement)
{
    public static RuntimePathRewriteResponse FromPolicy(BusinessRuntimePathRewritePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new RuntimePathRewriteResponse(policy.StripPrefix, policy.ReplacePrefix, policy.Replacement);
    }
}

public sealed record RuntimeRedirectResponse(
    int StatusCode,
    string TargetUrl,
    string TargetPath,
    bool PreserveQuery)
{
    public static RuntimeRedirectResponse FromPolicy(BusinessRuntimeRedirectPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new RuntimeRedirectResponse(
            policy.StatusCode,
            policy.TargetUrl,
            policy.TargetPath,
            policy.PreserveQuery);
    }
}

public sealed record RuntimeStaticResponseResponse(
    int StatusCode,
    string ContentType,
    string Body)
{
    public static RuntimeStaticResponseResponse FromResponse(BusinessRuntimeStaticResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return new RuntimeStaticResponseResponse(response.StatusCode, response.ContentType, response.Body);
    }
}

public sealed record RuntimeMaintenanceResponse(
    bool Enabled,
    int? RetryAfterSeconds,
    string ContentType,
    string Body)
{
    public static RuntimeMaintenanceResponse FromPolicy(BusinessRuntimeMaintenancePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new RuntimeMaintenanceResponse(
            policy.Enabled,
            policy.RetryAfterSeconds,
            policy.ContentType,
            policy.Body);
    }
}

public sealed record RuntimeCachePolicyResponse(
    bool Enabled,
    long MaxEntryBytes,
    long MaxTotalBytes,
    TimeSpan DefaultTtl,
    bool RespectOriginCacheControl,
    IReadOnlyList<string> VaryByHeaders,
    IReadOnlyList<int> CacheableStatusCodes,
    IReadOnlyList<string> Methods)
{
    public static RuntimeCachePolicyResponse FromPolicy(BusinessRuntimeCachePolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new RuntimeCachePolicyResponse(
            policy.Enabled,
            policy.MaxEntryBytes,
            policy.MaxTotalBytes,
            policy.DefaultTtl,
            policy.RespectOriginCacheControl,
            policy.VaryByHeaders.ToArray(),
            policy.CacheableStatusCodes.ToArray(),
            policy.Methods.ToArray());
    }
}

public sealed record RuntimeRouteResolvedOptionsResponse(
    long MaxRequestBodyBytes,
    TimeSpan ClientRequestHeadTimeout,
    TimeSpan UpstreamResponseHeadTimeout,
    bool AccessLogEnabled)
{
    public static RuntimeRouteResolvedOptionsResponse FromOptions(
        BusinessRuntimeRouteResolvedOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return new RuntimeRouteResolvedOptionsResponse(
            options.MaxRequestBodyBytes,
            options.ClientRequestHeadTimeout,
            options.UpstreamResponseHeadTimeout,
            options.AccessLogEnabled);
    }
}

public sealed record RuntimeRetryPolicyResponse(
    bool Enabled,
    int MaxAttempts,
    TimeSpan? PerAttemptTimeout,
    bool RetryOnConnectFailure,
    bool RetryOnUpstreamResponseHeadTimeout,
    IReadOnlyList<int> RetryOnStatusCodes,
    IReadOnlyList<string> RetryMethods,
    TimeSpan RetryBackoff)
{
    public static RuntimeRetryPolicyResponse FromPolicy(BusinessRuntimeRetryPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return new RuntimeRetryPolicyResponse(
            policy.Enabled,
            policy.MaxAttempts,
            policy.PerAttemptTimeout,
            policy.RetryOnConnectFailure,
            policy.RetryOnUpstreamResponseHeadTimeout,
            policy.RetryOnStatusCodes.ToArray(),
            policy.RetryMethods.ToArray(),
            policy.RetryBackoff);
    }
}

public enum RuntimeRouteActionResponse
{
    Proxy = 0,
    Redirect = 1,
    StaticResponse = 2
}

public static class RuntimeRouteActionResponseMapper
{
    public static RuntimeRouteActionResponse FromAction(BusinessRuntimeRouteAction action)
    {
        return action switch
        {
            BusinessRuntimeRouteAction.Proxy => RuntimeRouteActionResponse.Proxy,
            BusinessRuntimeRouteAction.Redirect => RuntimeRouteActionResponse.Redirect,
            BusinessRuntimeRouteAction.StaticResponse => RuntimeRouteActionResponse.StaticResponse,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };
    }
}
