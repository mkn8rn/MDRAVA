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
                if (listenersByKey.TryGetValue(key, out var existing))
                {
                    listenersByKey[key] = MergeListeners(existing, listener);
                }
                else
                {
                    listenersByKey.Add(key, listener);
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
        return $"{listener.Name}|{listener.Address}|{listener.Port}|{listener.Transport}";
    }

    private static ListenerOptions MergeListeners(ListenerOptions existing, ListenerOptions next)
    {
        var sniCertificates = existing.SniCertificates
            .Concat(next.SniCertificates)
            .ToList();

        return new ListenerOptions
        {
            Name = existing.Name,
            Address = existing.Address,
            Port = existing.Port,
            Enabled = existing.Enabled || next.Enabled,
            Transport = existing.Transport,
            DefaultCertificateId = !string.IsNullOrWhiteSpace(existing.DefaultCertificateId)
                ? existing.DefaultCertificateId
                : next.DefaultCertificateId,
            SniCertificates = sniCertificates,
            Backlog = existing.Backlog,
            MaxRequestHeadBytes = existing.MaxRequestHeadBytes,
            MaxResponseHeadBytes = existing.MaxResponseHeadBytes,
            MaxChunkLineBytes = existing.MaxChunkLineBytes,
            ForwardingBufferBytes = existing.ForwardingBufferBytes
        };
    }
}
