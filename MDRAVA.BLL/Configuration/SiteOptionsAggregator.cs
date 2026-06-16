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
                    listenersByKey.Add(key, CopyListener(listener));
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
                    HealthCheck = CopyHealthCheck(source.Site.HealthCheck),
                    Upstreams = CopyUpstreams(source.Site.Upstreams),
                    HttpsRedirect = CopyHttpsRedirect(source.Site.HttpsRedirect),
                    CanonicalHost = CopyCanonicalHost(source.Site.CanonicalHost),
                    HeaderPolicy = CopyHeaderPolicy(source.Site.HeaderPolicy),
                    Maintenance = CopyMaintenance(source.Site.Maintenance),
                    Cache = CopyCache(source.Site.Cache),
                    Retry = CopyRetry(source.Site.Retry),
                    Overrides = CopyOverrides(source.Site.Overrides)
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
                    HealthCheck = CopyHealthCheck(route.HealthCheck),
                    Upstreams = CopyUpstreams(route.Upstreams.Count > 0 ? route.Upstreams : source.Site.Upstreams),
                    HttpsRedirect = MergeHttpsRedirect(source.Site.HttpsRedirect, route.HttpsRedirect),
                    CanonicalHost = MergeCanonicalHost(source.Site.CanonicalHost, route.CanonicalHost),
                    HeaderPolicy = MergeHeaderPolicy(source.Site.HeaderPolicy, route.HeaderPolicy),
                    PathRewrite = CopyPathRewrite(route.PathRewrite),
                    Redirect = CopyRedirect(route.Redirect),
                    StaticResponse = CopyStaticResponse(route.StaticResponse),
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

    private static ListenerOptions CopyListener(ListenerOptions source)
    {
        return new ListenerOptions
        {
            Name = source.Name,
            Address = source.Address,
            Port = source.Port,
            Enabled = source.Enabled,
            Transport = source.Transport,
            Protocols = source.Protocols,
            Http3Enablement = source.Http3Enablement,
            Http3AltSvcEnabled = source.Http3AltSvcEnabled,
            Http3AltSvcMaxAgeSeconds = source.Http3AltSvcMaxAgeSeconds,
            DefaultCertificateId = source.DefaultCertificateId,
            SniCertificates = source.SniCertificates
                .Select(static binding => new SniCertificateOptions
                {
                    HostName = binding.HostName,
                    CertificateId = binding.CertificateId
                })
                .ToList(),
            Backlog = source.Backlog,
            MaxRequestHeadBytes = source.MaxRequestHeadBytes,
            MaxResponseHeadBytes = source.MaxResponseHeadBytes,
            MaxChunkLineBytes = source.MaxChunkLineBytes,
            ForwardingBufferBytes = source.ForwardingBufferBytes,
            Http2MaxConcurrentStreams = source.Http2MaxConcurrentStreams,
            Http2MaxHeaderListBytes = source.Http2MaxHeaderListBytes,
            Http2MaxFrameSize = source.Http2MaxFrameSize
        };
    }

    private static List<UpstreamOptions> CopyUpstreams(IEnumerable<UpstreamOptions> source)
    {
        return source.Select(CopyUpstream).ToList();
    }

    private static UpstreamOptions CopyUpstream(UpstreamOptions source)
    {
        return new UpstreamOptions
        {
            Name = source.Name,
            Scheme = source.Scheme,
            Protocol = source.Protocol,
            Address = source.Address,
            Port = source.Port,
            Weight = source.Weight,
            UpstreamTls = new UpstreamTlsOptions
            {
                ValidateCertificate = source.UpstreamTls.ValidateCertificate,
                SniHost = source.UpstreamTls.SniHost
            },
            CircuitBreaker = new ProxyCircuitBreakerOptions
            {
                Enabled = source.CircuitBreaker.Enabled,
                FailureThreshold = source.CircuitBreaker.FailureThreshold,
                SamplingWindowSeconds = source.CircuitBreaker.SamplingWindowSeconds,
                OpenDurationSeconds = source.CircuitBreaker.OpenDurationSeconds,
                HalfOpenMaxAttempts = source.CircuitBreaker.HalfOpenMaxAttempts,
                FailureStatusCodes = source.CircuitBreaker.FailureStatusCodes.ToList()
            }
        };
    }

    private static HealthCheckOptions CopyHealthCheck(HealthCheckOptions source)
    {
        return new HealthCheckOptions
        {
            Enabled = source.Enabled,
            Path = source.Path,
            IntervalSeconds = source.IntervalSeconds,
            TimeoutSeconds = source.TimeoutSeconds,
            HealthyThreshold = source.HealthyThreshold,
            UnhealthyThreshold = source.UnhealthyThreshold
        };
    }

    private static ProxyHttpsRedirectOptions CopyHttpsRedirect(ProxyHttpsRedirectOptions source)
    {
        return new ProxyHttpsRedirectOptions
        {
            Enabled = source.Enabled,
            StatusCode = source.StatusCode,
            HttpsPort = source.HttpsPort
        };
    }

    private static ProxyCanonicalHostOptions CopyCanonicalHost(ProxyCanonicalHostOptions source)
    {
        return new ProxyCanonicalHostOptions
        {
            Enabled = source.Enabled,
            TargetHost = source.TargetHost,
            StatusCode = source.StatusCode
        };
    }

    private static ProxyHeaderPolicyOptions CopyHeaderPolicy(ProxyHeaderPolicyOptions source)
    {
        return new ProxyHeaderPolicyOptions
        {
            SetRequestHeaders = CopyHeaderFields(source.SetRequestHeaders),
            RemoveRequestHeaders = source.RemoveRequestHeaders.ToList(),
            SetResponseHeaders = CopyHeaderFields(source.SetResponseHeaders),
            RemoveResponseHeaders = source.RemoveResponseHeaders.ToList()
        };
    }

    private static List<ProxyHeaderSetOptions> CopyHeaderFields(IEnumerable<ProxyHeaderSetOptions> source)
    {
        return source
            .Select(static header => new ProxyHeaderSetOptions
            {
                Name = header.Name,
                Value = header.Value
            })
            .ToList();
    }

    private static ProxyPathRewriteOptions CopyPathRewrite(ProxyPathRewriteOptions source)
    {
        return new ProxyPathRewriteOptions
        {
            StripPrefix = source.StripPrefix,
            ReplacePrefix = source.ReplacePrefix,
            Replacement = source.Replacement
        };
    }

    private static ProxyRedirectOptions CopyRedirect(ProxyRedirectOptions source)
    {
        return new ProxyRedirectOptions
        {
            StatusCode = source.StatusCode,
            TargetUrl = source.TargetUrl,
            TargetPath = source.TargetPath,
            PreserveQuery = source.PreserveQuery
        };
    }

    private static ProxyStaticResponseOptions CopyStaticResponse(ProxyStaticResponseOptions source)
    {
        return new ProxyStaticResponseOptions
        {
            StatusCode = source.StatusCode,
            ContentType = source.ContentType,
            Body = source.Body
        };
    }

    private static ProxyMaintenanceOptions CopyMaintenance(ProxyMaintenanceOptions source)
    {
        return new ProxyMaintenanceOptions
        {
            Enabled = source.Enabled,
            RetryAfterSeconds = source.RetryAfterSeconds,
            ContentType = source.ContentType,
            Body = source.Body
        };
    }

    private static ProxyCachePolicyOptions CopyCache(ProxyCachePolicyOptions source)
    {
        return new ProxyCachePolicyOptions
        {
            Enabled = source.Enabled,
            MaxEntryBytes = source.MaxEntryBytes,
            MaxTotalBytes = source.MaxTotalBytes,
            DefaultTtlSeconds = source.DefaultTtlSeconds,
            RespectOriginCacheControl = source.RespectOriginCacheControl,
            VaryByHeaders = source.VaryByHeaders.ToList(),
            CacheableStatusCodes = source.CacheableStatusCodes.ToList(),
            Methods = source.Methods.ToList()
        };
    }

    private static ProxyRetryPolicyOptions CopyRetry(ProxyRetryPolicyOptions source)
    {
        return new ProxyRetryPolicyOptions
        {
            Enabled = source.Enabled,
            MaxAttempts = source.MaxAttempts,
            PerAttemptTimeoutMs = source.PerAttemptTimeoutMs,
            RetryOnConnectFailure = source.RetryOnConnectFailure,
            RetryOnUpstreamResponseHeadTimeout = source.RetryOnUpstreamResponseHeadTimeout,
            RetryOnStatusCodes = source.RetryOnStatusCodes.ToList(),
            RetryMethods = source.RetryMethods.ToList(),
            RetryBackoffMilliseconds = source.RetryBackoffMilliseconds
        };
    }

    private static ProxyRouteOverrideOptions CopyOverrides(ProxyRouteOverrideOptions source)
    {
        return new ProxyRouteOverrideOptions
        {
            MaxRequestBodyBytes = source.MaxRequestBodyBytes,
            ClientRequestHeadTimeoutMs = source.ClientRequestHeadTimeoutMs,
            UpstreamResponseHeadTimeoutMs = source.UpstreamResponseHeadTimeoutMs,
            AccessLogEnabled = source.AccessLogEnabled
        };
    }
}
