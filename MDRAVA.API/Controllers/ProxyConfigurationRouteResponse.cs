using BusinessRuntimeHealthCheckProjection = MDRAVA.BLL.Configuration.RuntimeHealthCheckProjection;
using BusinessRuntimeRouteAction = MDRAVA.BLL.Configuration.RuntimeRouteAction;
using BusinessRuntimeRouteProjection = MDRAVA.BLL.Configuration.RuntimeRouteProjection;
using BusinessRuntimeRouteResolvedOptionsProjection = MDRAVA.BLL.Configuration.RuntimeRouteResolvedOptionsProjection;

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

    public static IReadOnlyList<RuntimeRouteResponse> FromRoutes(IReadOnlyList<BusinessRuntimeRouteProjection> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        return routes.Select(FromRoute).ToArray();
    }

    private static RuntimeRouteResponse FromRoute(BusinessRuntimeRouteProjection route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return new RuntimeRouteResponse(
            route.Name,
            route.Host,
            route.PathPrefix,
            RuntimeRouteActionResponseMapper.FromAction(route.Action),
            route.LoadBalancingPolicy,
            RuntimeHealthCheckResponse.FromProjection(route.HealthCheck),
            RuntimeUpstreamResponse.FromUpstreams(route.Upstreams),
            RuntimeHttpsRedirectResponse.FromProjection(route.HttpsRedirect),
            RuntimeCanonicalHostResponse.FromProjection(route.CanonicalHost),
            RuntimeHeaderPolicyResponse.FromPolicy(route.HeaderPolicy),
            RuntimePathRewriteResponse.FromPolicy(route.PathRewrite),
            RuntimeRedirectResponse.FromProjection(route.Redirect),
            RuntimeStaticResponseResponse.FromResponse(route.StaticResponse),
            RuntimeMaintenanceResponse.FromPolicy(route.Maintenance),
            RuntimeCachePolicyResponse.FromProjection(route.Cache),
            RuntimeRouteResolvedOptionsResponse.FromProjection(route.ResolvedOptions))
        {
            SiteName = route.SiteName,
            Retry = RuntimeRetryPolicyResponse.FromProjection(route.Retry)
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
    public static RuntimeHealthCheckResponse FromProjection(BusinessRuntimeHealthCheckProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeHealthCheckResponse(
            projection.Enabled,
            projection.Path,
            projection.Interval,
            projection.Timeout,
            projection.HealthyThreshold,
            projection.UnhealthyThreshold);
    }
}

public sealed record RuntimeRouteResolvedOptionsResponse(
    long MaxRequestBodyBytes,
    TimeSpan ClientRequestHeadTimeout,
    TimeSpan UpstreamResponseHeadTimeout,
    bool AccessLogEnabled)
{
    public static RuntimeRouteResolvedOptionsResponse FromProjection(
        BusinessRuntimeRouteResolvedOptionsProjection projection)
    {
        ArgumentNullException.ThrowIfNull(projection);

        return new RuntimeRouteResolvedOptionsResponse(
            projection.MaxRequestBodyBytes,
            projection.ClientRequestHeadTimeout,
            projection.UpstreamResponseHeadTimeout,
            projection.AccessLogEnabled);
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
