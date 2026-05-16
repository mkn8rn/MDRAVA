namespace MDRAVA.API.Proxy.Configuration;

public static class SiteOptionsAggregator
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
                if (!listenersByKey.TryAdd(key, listener))
                {
                    continue;
                }
            }

            routes.Add(new ProxyRouteOptions
            {
                Name = source.Site.Name,
                Host = source.Site.Host,
                PathPrefix = source.Site.PathPrefix,
                Upstreams = source.Site.Upstreams
            });
        }

        return new ProxyOptions
        {
            Listeners = listenersByKey.Values.ToList(),
            Routes = routes
        };
    }

    private static string GetListenerKey(ListenerOptions listener)
    {
        return $"{listener.Name}|{listener.Address}|{listener.Port}";
    }
}
