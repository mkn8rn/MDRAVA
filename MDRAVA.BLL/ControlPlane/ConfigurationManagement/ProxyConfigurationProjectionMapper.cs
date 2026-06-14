using System.Collections.ObjectModel;
using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Http3;

namespace MDRAVA.BLL.ControlPlane.ConfigurationManagement;

public static class ProxyConfigurationProjectionMapper
{
    public static ProxyConfigurationProjection ToProjection(
        ProxyConfigurationSnapshot snapshot,
        RuntimeHttp3SupportProjection http3)
    {
        var adminSecurity = new RuntimeAdminSecurityProjection(
            snapshot.AdminSecurity.Urls,
            snapshot.AdminSecurity.RequireAuthentication,
            snapshot.AdminSecurity.HasConfiguredToken,
            SecretRedactor.RedactConfiguredSecret(snapshot.AdminSecurity.HasConfiguredToken),
            snapshot.AdminSecurity.TokenEnvironmentVariable,
            snapshot.AdminSecurity.TokenSource,
            snapshot.AdminSecurity.RecentAuditCapacity);
        return new ProxyConfigurationProjection(
            snapshot.Version,
            snapshot.LoadedAtUtc,
            snapshot.SourceDirectory,
            snapshot.SourceFiles,
            snapshot.Discovery,
            adminSecurity,
            snapshot.Acme,
            snapshot.Timeouts,
            new RuntimeConnectionLimitsProjection(
                snapshot.ConnectionLimits.MaxRequestsPerClientConnection,
                snapshot.ConnectionLimits.MaxIdleUpstreamConnectionsPerUpstream,
                snapshot.ConnectionLimits.MaxActiveUpgradedTunnels),
            snapshot.Observability,
            new RuntimeLimitsProjection(
                snapshot.Limits.MaxActiveClientConnections,
                snapshot.Limits.MaxConcurrentTlsHandshakes,
                snapshot.Limits.RequestsPerMinutePerIp,
                snapshot.Limits.UpgradeRequestsPerMinutePerIp,
                snapshot.Limits.MaxRequestHeadBytes,
                snapshot.Limits.MaxHeaderCount,
                snapshot.Limits.MaxHeaderLineBytes,
                snapshot.Limits.MaxRequestBodyBytes,
                snapshot.Limits.MaxPathBytes,
                snapshot.Limits.ShutdownGracePeriod),
            snapshot.ForwardedHeaders,
            snapshot.Certificates.Values
                .Select(static certificate => new RuntimeCertificateProjection(
                    certificate.Id,
                    certificate.Path,
                    certificate.Format,
                    certificate.Source,
                    certificate.Domains,
                    certificate.HasConfiguredPassword,
                    certificate.Certificate.Subject,
                    certificate.Certificate.Thumbprint,
                    certificate.Certificate.NotBefore,
                    certificate.Certificate.NotAfter))
                .OrderBy(static certificate => certificate.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            new ReadOnlyCollection<RuntimeListenerProjection>(snapshot.Listeners
                .Select(static listener => new RuntimeListenerProjection(
                    listener.Name,
                    listener.Address,
                    listener.Port,
                    listener.Enabled,
                    listener.Transport,
                    listener.DefaultCertificateId,
                    listener.SniCertificates
                        .Select(static binding => new RuntimeSniCertificateBindingProjection(
                            binding.HostName,
                            binding.CertificateId))
                        .ToArray(),
                    listener.Backlog,
                    listener.MaxRequestHeadBytes,
                    listener.MaxResponseHeadBytes,
                    listener.MaxChunkLineBytes,
                    listener.ForwardingBufferBytes,
                    new RuntimeListenerIdentityProjection(
                        listener.Identity.Name,
                        listener.Identity.Address,
                        listener.Identity.Port,
                        listener.Identity.Transport,
                        listener.Identity.TlsEnabled,
                        listener.Identity.Key,
                        listener.Identity.BindKey),
                    listener.Protocols,
                    listener.Http3Enablement,
                    new RuntimeHttp3AltSvcProjection(
                        listener.Http3AltSvc.Enabled,
                        listener.Http3AltSvc.MaxAgeSeconds),
                    new RuntimeHttp2LimitsProjection(
                        listener.Http2Limits.MaxConcurrentStreams,
                        listener.Http2Limits.MaxHeaderListBytes,
                        listener.Http2Limits.MaxFrameSize),
                    listener.TcpTrafficEnabled,
                    listener.Http3ProtocolConfigured,
                    ToQuicListenerIdentityProjection(listener.QuicIdentity),
                    new RuntimeHttp3ListenerReadinessProjection(
                        listener.Http3.Configured,
                        listener.Http3.DefaultEnabled,
                        listener.Http3.EnablementLevel,
                        listener.Http3.EnabledForTraffic,
                        listener.Http3.DisabledReason,
                        listener.Http3.AltSvcConfigured,
                        listener.Http3.AltSvcMaxAgeSeconds,
                        listener.Http3.UdpQuicListenerIdentityModeled,
                        ToQuicListenerIdentityProjection(listener.Http3.QuicIdentity))))
                .ToArray()),
            new ReadOnlyCollection<RuntimeRouteProjection>(snapshot.Routes
                .Select(static route => new RuntimeRouteProjection(
                    route.Name,
                    route.Host,
                    route.PathPrefix,
                    route.Action,
                    route.LoadBalancingPolicy,
                    new RuntimeHealthCheckProjection(
                        route.HealthCheck.Enabled,
                        route.HealthCheck.Path,
                        route.HealthCheck.Interval,
                        route.HealthCheck.Timeout,
                        route.HealthCheck.HealthyThreshold,
                        route.HealthCheck.UnhealthyThreshold),
                    new ReadOnlyCollection<RuntimeUpstreamProjection>(route.Upstreams
                        .Select(static upstream => new RuntimeUpstreamProjection(
                            upstream.RouteName,
                            upstream.Name,
                            upstream.Scheme,
                            upstream.Protocol,
                            upstream.Address,
                            upstream.Port,
                            upstream.Weight,
                            new RuntimeUpstreamTlsProjection(
                                upstream.Tls.ValidateCertificate,
                                upstream.Tls.SniHost),
                            upstream.Endpoint,
                            upstream.UriEndpoint,
                            upstream.EffectiveSniHost,
                            upstream.Identity,
                            new RuntimeCircuitBreakerProjection(
                                upstream.CircuitBreaker.Enabled,
                                upstream.CircuitBreaker.FailureThreshold,
                                upstream.CircuitBreaker.SamplingWindow,
                                upstream.CircuitBreaker.OpenDuration,
                                upstream.CircuitBreaker.HalfOpenMaxAttempts,
                                upstream.CircuitBreaker.FailureStatusCodes)))
                        .ToArray()),
                    new RuntimeHttpsRedirectProjection(
                        route.HttpsRedirect.Enabled,
                        route.HttpsRedirect.StatusCode,
                        route.HttpsRedirect.HttpsPort),
                    new RuntimeCanonicalHostProjection(
                        route.CanonicalHost.Enabled,
                        route.CanonicalHost.TargetHost,
                        route.CanonicalHost.StatusCode),
                    new RuntimeHeaderPolicyProjection(
                        route.HeaderPolicy.SetRequestHeaders
                            .Select(static header => new RuntimeHeaderFieldProjection(header.Name, header.Value))
                            .ToArray(),
                        route.HeaderPolicy.RemoveRequestHeaders,
                        route.HeaderPolicy.SetResponseHeaders
                            .Select(static header => new RuntimeHeaderFieldProjection(header.Name, header.Value))
                            .ToArray(),
                        route.HeaderPolicy.RemoveResponseHeaders),
                    new RuntimePathRewriteProjection(
                        route.PathRewrite.StripPrefix,
                        route.PathRewrite.ReplacePrefix,
                        route.PathRewrite.Replacement),
                    new RuntimeRedirectProjection(
                        route.Redirect.StatusCode,
                        route.Redirect.TargetUrl,
                        route.Redirect.TargetPath,
                        route.Redirect.PreserveQuery),
                    new RuntimeStaticResponseProjection(
                        route.StaticResponse.StatusCode,
                        route.StaticResponse.ContentType,
                        route.StaticResponse.Body),
                    new RuntimeMaintenanceProjection(
                        route.Maintenance.Enabled,
                        route.Maintenance.RetryAfterSeconds,
                        route.Maintenance.ContentType,
                        route.Maintenance.Body),
                    new RuntimeCacheProjection(
                        route.Cache.Enabled,
                        route.Cache.MaxEntryBytes,
                        route.Cache.MaxTotalBytes,
                        route.Cache.DefaultTtl,
                        route.Cache.RespectOriginCacheControl,
                        route.Cache.VaryByHeaders,
                        route.Cache.CacheableStatusCodes,
                        route.Cache.Methods),
                    new RuntimeRouteResolvedOptionsProjection(
                        route.ResolvedOptions.MaxRequestBodyBytes,
                        route.ResolvedOptions.ClientRequestHeadTimeout,
                        route.ResolvedOptions.UpstreamResponseHeadTimeout,
                        route.ResolvedOptions.AccessLogEnabled),
                    route.SiteName,
                    new RuntimeRetryProjection(
                        route.Retry.Enabled,
                        route.Retry.MaxAttempts,
                        route.Retry.PerAttemptTimeout,
                        route.Retry.RetryOnConnectFailure,
                        route.Retry.RetryOnUpstreamResponseHeadTimeout,
                        route.Retry.RetryOnStatusCodes,
                        route.Retry.RetryMethods,
                        route.Retry.RetryBackoff)))
                .ToArray()))
        {
            Metrics = snapshot.Metrics,
            Http3 = http3
        };
    }

    private static RuntimeQuicListenerIdentityProjection? ToQuicListenerIdentityProjection(
        RuntimeQuicListenerIdentity? identity)
    {
        return identity is null
            ? null
            : new RuntimeQuicListenerIdentityProjection(
                identity.Name,
                identity.Address,
                identity.Port,
                identity.TlsEnabled,
                identity.Key,
                identity.BindKey);
    }
}
