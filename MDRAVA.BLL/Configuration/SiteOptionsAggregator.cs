namespace MDRAVA.BLL.Configuration;

public static partial class SiteOptionsAggregator
{
    public static ProxyOptions ToProxyOptions(IEnumerable<SiteConfigurationSource> sources)
    {
        Dictionary<string, ListenerOptions> listenersByKey = new(StringComparer.OrdinalIgnoreCase);
        List<ProxyRouteOptions> routes = [];

        foreach (var source in sources)
        {
            foreach (var listener in source.Site.Listeners)
            {
                var key = GetListenerKey(listener);
                if (listenersByKey.TryGetValue(key, out var existing))
                {
                    listenersByKey[key] = MergeListeners(existing, listener);
                }
                else
                {
                    listenersByKey.Add(key, listener);
                }
            }

            if (source.Site.Routes.Count == 0)
            {
                routes.Add(new ProxyRouteOptions
                {
                    Name = source.Site.Name,
                    SiteName = source.Site.Name,
                    Host = source.Site.Host,
                    PathPrefix = source.Site.PathPrefix,
                    Action = "proxy",
                    LoadBalancingPolicy = source.Site.LoadBalancingPolicy,
                    HealthCheck = source.Site.HealthCheck,
                    Upstreams = source.Site.Upstreams,
                    HttpsRedirect = source.Site.HttpsRedirect,
                    CanonicalHost = source.Site.CanonicalHost,
                    HeaderPolicy = source.Site.HeaderPolicy,
                    Maintenance = source.Site.Maintenance,
                    Cache = source.Site.Cache,
                    Retry = source.Site.Retry,
                    Overrides = source.Site.Overrides
                });
                continue;
            }

            foreach (var route in source.Site.Routes)
            {
                routes.Add(new ProxyRouteOptions
                {
                    Name = route.Name,
                    SiteName = source.Site.Name,
                    Host = string.IsNullOrWhiteSpace(route.Host) || string.Equals(route.Host, "*", StringComparison.Ordinal)
                        ? source.Site.Host
                        : route.Host,
                    PathPrefix = route.PathPrefix,
                    Action = route.Action,
                    LoadBalancingPolicy = string.IsNullOrWhiteSpace(route.LoadBalancingPolicy)
                        ? source.Site.LoadBalancingPolicy
                        : route.LoadBalancingPolicy,
                    HealthCheck = route.HealthCheck,
                    Upstreams = route.Upstreams.Count > 0 ? route.Upstreams : source.Site.Upstreams,
                    HttpsRedirect = MergeHttpsRedirect(source.Site.HttpsRedirect, route.HttpsRedirect),
                    CanonicalHost = MergeCanonicalHost(source.Site.CanonicalHost, route.CanonicalHost),
                    HeaderPolicy = MergeHeaderPolicy(source.Site.HeaderPolicy, route.HeaderPolicy),
                    PathRewrite = route.PathRewrite,
                    Redirect = route.Redirect,
                    StaticResponse = route.StaticResponse,
                    Maintenance = MergeMaintenance(source.Site.Maintenance, route.Maintenance),
                    Cache = MergeCache(source.Site.Cache, route.Cache),
                    Retry = MergeRetry(source.Site.Retry, route.Retry),
                    Overrides = MergeOverrides(source.Site.Overrides, route.Overrides)
                });
            }
        }

        return new ProxyOptions
        {
            Listeners = listenersByKey.Values.ToList(),
            Routes = routes
        };
    }
}
