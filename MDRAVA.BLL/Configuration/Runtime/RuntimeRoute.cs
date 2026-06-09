namespace MDRAVA.BLL.Configuration;

public sealed record RuntimeRoute(
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
    public string SiteName { get; init; } = "";

    public RuntimeRetryPolicy Retry { get; init; } = RuntimeRetryPolicy.Disabled;
}
