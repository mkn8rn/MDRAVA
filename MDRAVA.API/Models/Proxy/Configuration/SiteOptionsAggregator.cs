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
            Protocols = MergeListenerProtocols(existing.Protocols, next.Protocols),
            ExperimentalHttp3 = existing.ExperimentalHttp3 || next.ExperimentalHttp3,
            Http3Enablement = MergeHttp3Enablement(existing.Http3Enablement, next.Http3Enablement),
            Http3AltSvcEnabled = existing.Http3AltSvcEnabled || next.Http3AltSvcEnabled,
            Http3AltSvcMaxAgeSeconds = existing.Http3AltSvcMaxAgeSeconds,
            DefaultCertificateId = !string.IsNullOrWhiteSpace(existing.DefaultCertificateId)
                ? existing.DefaultCertificateId
                : next.DefaultCertificateId,
            SniCertificates = sniCertificates,
            Backlog = existing.Backlog,
            MaxRequestHeadBytes = existing.MaxRequestHeadBytes,
            MaxResponseHeadBytes = existing.MaxResponseHeadBytes,
            MaxChunkLineBytes = existing.MaxChunkLineBytes,
            ForwardingBufferBytes = existing.ForwardingBufferBytes,
            Http3MaxBufferedRequestBodyBytes = existing.Http3MaxBufferedRequestBodyBytes,
            Http2MaxConcurrentStreams = existing.Http2MaxConcurrentStreams,
            Http2MaxHeaderListBytes = existing.Http2MaxHeaderListBytes,
            Http2MaxFrameSize = existing.Http2MaxFrameSize
        };
    }

    private static string MergeHttp3Enablement(string existing, string next)
    {
        if (string.Equals(existing, "disabled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(next, "disabled", StringComparison.OrdinalIgnoreCase))
        {
            return "disabled";
        }

        if (string.Equals(existing, "beta", StringComparison.OrdinalIgnoreCase)
            || string.Equals(next, "beta", StringComparison.OrdinalIgnoreCase))
        {
            return "beta";
        }

        if (string.Equals(existing, "preview", StringComparison.OrdinalIgnoreCase)
            || string.Equals(next, "preview", StringComparison.OrdinalIgnoreCase))
        {
            return "preview";
        }

        return string.Equals(existing, "default", StringComparison.OrdinalIgnoreCase)
            || string.Equals(next, "default", StringComparison.OrdinalIgnoreCase)
            ? "default"
            : "";
    }

    private static string MergeListenerProtocols(string existing, string next)
    {
        if (!TryParseListenerProtocols(existing, out var existingProtocols))
        {
            return existing;
        }

        if (!TryParseListenerProtocols(next, out var nextProtocols))
        {
            return next;
        }

        var merged = existingProtocols | nextProtocols;
        return ListenerProtocolsText(merged);
    }

    private static bool TryParseListenerProtocols(
        string protocols,
        out RuntimeListenerProtocols parsed)
    {
        parsed = protocols.Trim().ToLowerInvariant() switch
        {
            "http2" => RuntimeListenerProtocols.Http2,
            "http1andhttp2" => RuntimeListenerProtocols.Http1AndHttp2,
            "http3preview" => RuntimeListenerProtocols.Http3Preview,
            "http1andhttp3preview" => RuntimeListenerProtocols.Http1AndHttp3Preview,
            "http2andhttp3preview" => RuntimeListenerProtocols.Http2AndHttp3Preview,
            "http1andhttp2andhttp3preview" => RuntimeListenerProtocols.Http1AndHttp2AndHttp3Preview,
            "http1" => RuntimeListenerProtocols.Http1,
            _ => RuntimeListenerProtocols.None
        };
        return parsed != RuntimeListenerProtocols.None;
    }

    private static string ListenerProtocolsText(RuntimeListenerProtocols protocols)
    {
        return protocols switch
        {
            RuntimeListenerProtocols.Http2 => "http2",
            RuntimeListenerProtocols.Http1AndHttp2 => "http1AndHttp2",
            RuntimeListenerProtocols.Http3Preview => "http3Preview",
            RuntimeListenerProtocols.Http1AndHttp3Preview => "http1AndHttp3Preview",
            RuntimeListenerProtocols.Http2AndHttp3Preview => "http2AndHttp3Preview",
            RuntimeListenerProtocols.Http1AndHttp2AndHttp3Preview => "http1AndHttp2AndHttp3Preview",
            _ => "http1"
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

    private static ProxyCachePolicyOptions MergeCache(
        ProxyCachePolicyOptions site,
        ProxyCachePolicyOptions route)
    {
        return route.Enabled ? route : site;
    }

    private static ProxyRetryPolicyOptions MergeRetry(
        ProxyRetryPolicyOptions site,
        ProxyRetryPolicyOptions route)
    {
        return route.Enabled ? route : site;
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
