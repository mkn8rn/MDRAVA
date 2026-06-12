using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public static class ProxyConfigLintConfigurationSnapshotMapper
{
    public static ProxyConfigLintConfigurationSnapshot ToLintSnapshot(
        ProxyConfigurationSnapshot snapshot,
        RuntimeHttp3PlatformSupport platformSupport)
    {
        var http3 = Http3RuntimeSupport.ProjectConfiguration(
            Http3SupportSourceMapper.FromConfiguration(snapshot.Listeners, snapshot.Routes),
            platformSupport);
        return new ProxyConfigLintConfigurationSnapshot(
            snapshot.SourceFiles,
            new ProxyConfigLintAdminSecurity(
                snapshot.AdminSecurity.Urls,
                snapshot.AdminSecurity.RequireAuthentication),
            new ProxyConfigLintMetricsOptions(snapshot.Metrics.PublicMetricsEnabled),
            http3.QuicConnectionSupported,
            snapshot.Listeners
                .Select(static listener => new ProxyConfigLintListener(
                    listener.Name,
                    listener.Address,
                    listener.Port,
                    listener.Enabled,
                    listener.Transport.ToString(),
                    listener.Http3.Configured,
                    listener.Http3.EnabledForTraffic,
                    listener.Http3.DisabledReason,
                    listener.Http3.EnablementLevel,
                    RuntimeHttp3AltSvcPolicy.IsEnabled(listener),
                    listener.QuicIdentity?.Key))
                .ToArray(),
            snapshot.Routes
                .Select(static route => new ProxyConfigLintRoute(
                    route.Name,
                    route.SiteName,
                    route.Host,
                    route.PathPrefix,
                    route.Action.ToString(),
                    route.HttpsRedirect.Enabled,
                    route.CanonicalHost.Enabled,
                    route.CanonicalHost.TargetHost,
                    route.Cache.Enabled,
                    route.Cache.VaryByHeaders,
                    route.Retry.Enabled,
                    route.Retry.RetryMethods,
                    route.HealthCheck.Enabled,
                    route.Upstreams
                        .Select(static upstream => new ProxyConfigLintUpstream(
                            upstream.Name,
                            upstream.Scheme,
                            upstream.Protocol,
                            upstream.Tls.ValidateCertificate,
                            upstream.CircuitBreaker.Enabled))
                        .ToArray(),
                    route.StaticResponse.Body))
                .ToArray());
    }
}
