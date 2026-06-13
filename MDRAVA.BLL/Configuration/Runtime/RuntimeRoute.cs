namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeRoute
{
    public RuntimeRoute(
        string Name,
        string Host,
        string PathPrefix,
        RuntimeRouteAction Action,
        string LoadBalancingPolicy,
        RuntimeHealthCheckOptions HealthCheck,
        IReadOnlyList<RuntimeUpstream> Upstreams,
        RuntimeHttpsRedirectPolicy HttpsRedirect,
        RuntimeCanonicalHostPolicy CanonicalHost,
        RuntimeHeaderPolicy HeaderPolicy,
        RuntimePathRewritePolicy PathRewrite,
        RuntimeRedirectPolicy Redirect,
        RuntimeStaticResponse StaticResponse,
        RuntimeMaintenancePolicy Maintenance,
        RuntimeCachePolicy Cache,
        RuntimeRouteResolvedOptions ResolvedOptions)
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
    }

    public string Name { get; init; }

    public string Host { get; init; }

    public string PathPrefix { get; init; }

    public RuntimeRouteAction Action { get; init; }

    public string LoadBalancingPolicy { get; init; }

    public RuntimeHealthCheckOptions HealthCheck { get; init; }

    public IReadOnlyList<RuntimeUpstream> Upstreams { get; }

    public RuntimeHttpsRedirectPolicy HttpsRedirect { get; init; }

    public RuntimeCanonicalHostPolicy CanonicalHost { get; init; }

    public RuntimeHeaderPolicy HeaderPolicy { get; init; }

    public RuntimePathRewritePolicy PathRewrite { get; init; }

    public RuntimeRedirectPolicy Redirect { get; init; }

    public RuntimeStaticResponse StaticResponse { get; init; }

    public RuntimeMaintenancePolicy Maintenance { get; init; }

    public RuntimeCachePolicy Cache { get; init; }

    public RuntimeRouteResolvedOptions ResolvedOptions { get; init; }

    public string SiteName { get; init; } = "";

    public RuntimeRetryPolicy Retry { get; init; } = RuntimeRetryPolicy.Disabled;

    public RuntimeRoute WithUpstreams(IReadOnlyList<RuntimeUpstream> upstreams)
    {
        return new RuntimeRoute(
            Name,
            Host,
            PathPrefix,
            Action,
            LoadBalancingPolicy,
            HealthCheck,
            upstreams,
            HttpsRedirect,
            CanonicalHost,
            HeaderPolicy,
            PathRewrite,
            Redirect,
            StaticResponse,
            Maintenance,
            Cache,
            ResolvedOptions)
        {
            SiteName = SiteName,
            Retry = Retry
        };
    }
}
