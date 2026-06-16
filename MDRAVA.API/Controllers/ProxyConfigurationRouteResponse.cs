using BusinessRuntimeHealthCheckProjection = MDRAVA.BLL.Configuration.RuntimeHealthCheckProjection;
using BusinessRuntimeRouteAction = MDRAVA.BLL.Configuration.RuntimeRouteAction;
using BusinessRuntimeRouteProjection = MDRAVA.BLL.Configuration.RuntimeRouteProjection;
using BusinessRuntimeRouteResolvedOptionsProjection = MDRAVA.BLL.Configuration.RuntimeRouteResolvedOptionsProjection;

namespace MDRAVA.API.Controllers;

public sealed record RuntimeRouteResponse
{
    public RuntimeRouteResponse(
        string name,
        string host,
        string pathPrefix,
        RuntimeRouteActionResponse action,
        string loadBalancingPolicy,
        RuntimeHealthCheckResponse healthCheck,
        IReadOnlyList<RuntimeUpstreamResponse> upstreams,
        RuntimeHttpsRedirectResponse httpsRedirect,
        RuntimeCanonicalHostResponse canonicalHost,
        RuntimeHeaderPolicyResponse headerPolicy,
        RuntimePathRewriteResponse pathRewrite,
        RuntimeRedirectResponse redirect,
        RuntimeStaticResponseResponse staticResponse,
        RuntimeMaintenanceResponse maintenance,
        RuntimeCachePolicyResponse cache,
        RuntimeRouteResolvedOptionsResponse resolvedOptions,
        string siteName,
        RuntimeRetryPolicyResponse retry)
    {
        ArgumentNullException.ThrowIfNull(healthCheck);
        ArgumentNullException.ThrowIfNull(httpsRedirect);
        ArgumentNullException.ThrowIfNull(canonicalHost);
        ArgumentNullException.ThrowIfNull(headerPolicy);
        ArgumentNullException.ThrowIfNull(pathRewrite);
        ArgumentNullException.ThrowIfNull(redirect);
        ArgumentNullException.ThrowIfNull(staticResponse);
        ArgumentNullException.ThrowIfNull(maintenance);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(resolvedOptions);
        ArgumentNullException.ThrowIfNull(retry);

        Name = name;
        Host = host;
        PathPrefix = pathPrefix;
        Action = action;
        LoadBalancingPolicy = loadBalancingPolicy;
        HealthCheck = healthCheck;
        Upstreams = ApiResponseList.Copy(upstreams);
        HttpsRedirect = httpsRedirect;
        CanonicalHost = canonicalHost;
        HeaderPolicy = headerPolicy;
        PathRewrite = pathRewrite;
        Redirect = redirect;
        StaticResponse = staticResponse;
        Maintenance = maintenance;
        Cache = cache;
        ResolvedOptions = resolvedOptions;
        SiteName = siteName;
        Retry = retry;
    }

    public string Name { get; }

    public string Host { get; }

    public string PathPrefix { get; }

    public RuntimeRouteActionResponse Action { get; }

    public string LoadBalancingPolicy { get; }

    public RuntimeHealthCheckResponse HealthCheck { get; }

    public IReadOnlyList<RuntimeUpstreamResponse> Upstreams { get; }

    public RuntimeHttpsRedirectResponse HttpsRedirect { get; }

    public RuntimeCanonicalHostResponse CanonicalHost { get; }

    public RuntimeHeaderPolicyResponse HeaderPolicy { get; }

    public RuntimePathRewriteResponse PathRewrite { get; }

    public RuntimeRedirectResponse Redirect { get; }

    public RuntimeStaticResponseResponse StaticResponse { get; }

    public RuntimeMaintenanceResponse Maintenance { get; }

    public RuntimeCachePolicyResponse Cache { get; }

    public RuntimeRouteResolvedOptionsResponse ResolvedOptions { get; }

    public string SiteName { get; }

    public RuntimeRetryPolicyResponse Retry { get; }

    public static IReadOnlyList<RuntimeRouteResponse> FromRoutes(IEnumerable<BusinessRuntimeRouteProjection> routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        return ApiResponseList.Copy(routes.Select(FromRoute));
    }

    private static RuntimeRouteResponse FromRoute(BusinessRuntimeRouteProjection route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return new RuntimeRouteResponse(
            name: route.Name,
            host: route.Host,
            pathPrefix: route.PathPrefix,
            action: RuntimeRouteActionResponseMapper.FromAction(route.Action),
            loadBalancingPolicy: route.LoadBalancingPolicy,
            healthCheck: RuntimeHealthCheckResponse.FromProjection(route.HealthCheck),
            upstreams: RuntimeUpstreamResponse.FromUpstreams(route.Upstreams),
            httpsRedirect: RuntimeHttpsRedirectResponse.FromProjection(route.HttpsRedirect),
            canonicalHost: RuntimeCanonicalHostResponse.FromProjection(route.CanonicalHost),
            headerPolicy: RuntimeHeaderPolicyResponse.FromProjection(route.HeaderPolicy),
            pathRewrite: RuntimePathRewriteResponse.FromProjection(route.PathRewrite),
            redirect: RuntimeRedirectResponse.FromProjection(route.Redirect),
            staticResponse: RuntimeStaticResponseResponse.FromProjection(route.StaticResponse),
            maintenance: RuntimeMaintenanceResponse.FromProjection(route.Maintenance),
            cache: RuntimeCachePolicyResponse.FromProjection(route.Cache),
            resolvedOptions: RuntimeRouteResolvedOptionsResponse.FromProjection(route.ResolvedOptions),
            siteName: route.SiteName,
            retry: RuntimeRetryPolicyResponse.FromProjection(route.Retry));
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
