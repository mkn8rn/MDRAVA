using BusinessRuntimeCachePolicy = MDRAVA.BLL.Configuration.RuntimeCachePolicy;
using BusinessRuntimeHealthCheckOptions = MDRAVA.BLL.Configuration.RuntimeHealthCheckOptions;
using BusinessRuntimeRetryPolicy = MDRAVA.BLL.Configuration.RuntimeRetryPolicy;
using BusinessRuntimeRoute = MDRAVA.BLL.Configuration.RuntimeRoute;
using BusinessRuntimeRouteAction = MDRAVA.BLL.Configuration.RuntimeRouteAction;
using BusinessRuntimeRouteResolvedOptions = MDRAVA.BLL.Configuration.RuntimeRouteResolvedOptions;

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
