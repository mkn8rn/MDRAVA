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
                    route.HealthCheck,
                    route.Upstreams,
                    route.HttpsRedirect,
                    route.CanonicalHost,
                    route.HeaderPolicy,
                    route.PathRewrite,
                    route.Redirect,
                    route.StaticResponse,
                    route.Maintenance,
                    route.Cache,
                    route.ResolvedOptions,
                    route.SiteName,
                    route.Retry))
                .ToArray()))
        {
            Metrics = snapshot.Metrics,
            Http3 = http3
        };
    }
}
