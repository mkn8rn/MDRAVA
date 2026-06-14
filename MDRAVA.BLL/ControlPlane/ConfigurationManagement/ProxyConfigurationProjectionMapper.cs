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
            snapshot.ConnectionLimits,
            snapshot.Observability,
            snapshot.Limits,
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
                    listener.SniCertificates,
                    listener.Backlog,
                    listener.MaxRequestHeadBytes,
                    listener.MaxResponseHeadBytes,
                    listener.MaxChunkLineBytes,
                    listener.ForwardingBufferBytes,
                    listener.Identity,
                    listener.Protocols,
                    listener.Http3Enablement,
                    listener.Http3AltSvc,
                    listener.Http2Limits,
                    listener.TcpTrafficEnabled,
                    listener.Http3ProtocolConfigured,
                    listener.QuicIdentity,
                    listener.Http3))
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
                    route.HeaderPolicy,
                    route.PathRewrite,
                    new RuntimeRedirectProjection(
                        route.Redirect.StatusCode,
                        route.Redirect.TargetUrl,
                        route.Redirect.TargetPath,
                        route.Redirect.PreserveQuery),
                    route.StaticResponse,
                    route.Maintenance,
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
}
