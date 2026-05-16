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

            if (source.Site.Routes.Count == 0)
            {
                routes.Add(new ProxyRouteOptions
                {
                    Name = source.Site.Name,
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
                    Overrides = source.Site.Overrides
                });
                continue;
            }

            foreach (var route in source.Site.Routes)
            {
                routes.Add(new ProxyRouteOptions
                {
                    Name = route.Name,
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

    private static ProxyHttpsRedirectOptions MergeHttpsRedirect(
        ProxyHttpsRedirectOptions site,
        ProxyHttpsRedirectOptions route)
    {
        return new ProxyHttpsRedirectOptions
        {
            Enabled = route.Enabled ?? site.Enabled,
            StatusCode = route.StatusCode ?? site.StatusCode,
            HttpsPort = route.HttpsPort ?? site.HttpsPort
        };
    }

    private static ProxyCanonicalHostOptions MergeCanonicalHost(
        ProxyCanonicalHostOptions site,
        ProxyCanonicalHostOptions route)
    {
        return new ProxyCanonicalHostOptions
        {
            Enabled = route.Enabled ?? site.Enabled,
            TargetHost = string.IsNullOrWhiteSpace(route.TargetHost) ? site.TargetHost : route.TargetHost,
            StatusCode = route.StatusCode ?? site.StatusCode
        };
    }

    private static ProxyHeaderPolicyOptions MergeHeaderPolicy(
        ProxyHeaderPolicyOptions site,
        ProxyHeaderPolicyOptions route)
    {
        return new ProxyHeaderPolicyOptions
        {
            SetRequestHeaders = site.SetRequestHeaders.Concat(route.SetRequestHeaders).ToList(),
            RemoveRequestHeaders = site.RemoveRequestHeaders.Concat(route.RemoveRequestHeaders).ToList(),
            SetResponseHeaders = site.SetResponseHeaders.Concat(route.SetResponseHeaders).ToList(),
            RemoveResponseHeaders = site.RemoveResponseHeaders.Concat(route.RemoveResponseHeaders).ToList()
        };
    }

    private static ProxyMaintenanceOptions MergeMaintenance(
        ProxyMaintenanceOptions site,
        ProxyMaintenanceOptions route)
    {
        return new ProxyMaintenanceOptions
        {
            Enabled = route.Enabled ?? site.Enabled,
            RetryAfterSeconds = route.RetryAfterSeconds ?? site.RetryAfterSeconds,
            ContentType = string.IsNullOrWhiteSpace(route.ContentType) || string.Equals(route.ContentType, "text/plain; charset=utf-8", StringComparison.OrdinalIgnoreCase)
                ? site.ContentType
                : route.ContentType,
            Body = string.Equals(route.Body, "Service Unavailable", StringComparison.Ordinal)
                ? site.Body
                : route.Body
        };
    }

    private static ProxyRouteOverrideOptions MergeOverrides(
        ProxyRouteOverrideOptions site,
        ProxyRouteOverrideOptions route)
    {
        return new ProxyRouteOverrideOptions
        {
            MaxRequestBodyBytes = route.MaxRequestBodyBytes ?? site.MaxRequestBodyBytes,
            ClientRequestHeadTimeoutMs = route.ClientRequestHeadTimeoutMs ?? site.ClientRequestHeadTimeoutMs,
            UpstreamResponseHeadTimeoutMs = route.UpstreamResponseHeadTimeoutMs ?? site.UpstreamResponseHeadTimeoutMs,
            AccessLogEnabled = route.AccessLogEnabled ?? site.AccessLogEnabled
        };
    }
}
