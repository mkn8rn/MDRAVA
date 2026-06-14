namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeRouteProjection
{
    public RuntimeRouteProjection(
        string Name,
        string Host,
        string PathPrefix,
        RuntimeRouteAction Action,
        string LoadBalancingPolicy,
        RuntimeHealthCheckProjection HealthCheck,
        IReadOnlyList<RuntimeUpstreamProjection> Upstreams,
        RuntimeHttpsRedirectProjection HttpsRedirect,
        RuntimeCanonicalHostPolicy CanonicalHost,
        RuntimeHeaderPolicy HeaderPolicy,
        RuntimePathRewritePolicy PathRewrite,
        RuntimeRedirectPolicy Redirect,
        RuntimeStaticResponse StaticResponse,
        RuntimeMaintenancePolicy Maintenance,
        RuntimeCacheProjection Cache,
        RuntimeRouteResolvedOptionsProjection ResolvedOptions,
        string SiteName,
        RuntimeRetryProjection Retry)
    {
        this.Name = Name;
        this.Host = Host;
        this.PathPrefix = PathPrefix;
        this.Action = Action;
        this.LoadBalancingPolicy = LoadBalancingPolicy;
        this.HealthCheck = HealthCheck;
        this.Upstreams = RuntimeList.Copy(Upstreams);
        this.HttpsRedirect = HttpsRedirect;
        this.CanonicalHost = CanonicalHost;
        this.HeaderPolicy = HeaderPolicy;
        this.PathRewrite = PathRewrite;
        this.Redirect = Redirect;
        this.StaticResponse = StaticResponse;
        this.Maintenance = Maintenance;
        this.Cache = Cache;
        this.ResolvedOptions = ResolvedOptions;
        this.SiteName = SiteName;
        this.Retry = Retry;
    }

    public string Name { get; init; }

    public string Host { get; init; }

    public string PathPrefix { get; init; }

    public RuntimeRouteAction Action { get; init; }

    public string LoadBalancingPolicy { get; init; }

    public RuntimeHealthCheckProjection HealthCheck { get; init; }

    public IReadOnlyList<RuntimeUpstreamProjection> Upstreams { get; }

    public RuntimeHttpsRedirectProjection HttpsRedirect { get; init; }

    public RuntimeCanonicalHostPolicy CanonicalHost { get; init; }

    public RuntimeHeaderPolicy HeaderPolicy { get; init; }

    public RuntimePathRewritePolicy PathRewrite { get; init; }

    public RuntimeRedirectPolicy Redirect { get; init; }

    public RuntimeStaticResponse StaticResponse { get; init; }

    public RuntimeMaintenancePolicy Maintenance { get; init; }

    public RuntimeCacheProjection Cache { get; init; }

    public RuntimeRouteResolvedOptionsProjection ResolvedOptions { get; init; }

    public string SiteName { get; init; }

    public RuntimeRetryProjection Retry { get; init; }
}
