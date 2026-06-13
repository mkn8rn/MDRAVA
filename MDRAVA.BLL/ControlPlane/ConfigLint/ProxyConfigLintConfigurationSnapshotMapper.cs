using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public sealed record ProxyConfigLintRuntimeConfigurationSource
{
    public ProxyConfigLintRuntimeConfigurationSource(
        IReadOnlyList<string> SourceFiles,
        IReadOnlyList<string> AdminUrls,
        bool AdminRequiresAuthentication,
        bool PublicMetricsEnabled,
        IReadOnlyList<RuntimeListener> Listeners,
        IReadOnlyList<RuntimeRoute> Routes)
    {
        ArgumentNullException.ThrowIfNull(SourceFiles);
        ArgumentNullException.ThrowIfNull(AdminUrls);
        ArgumentNullException.ThrowIfNull(Listeners);
        ArgumentNullException.ThrowIfNull(Routes);

        this.SourceFiles = ConfigLintList.Copy(SourceFiles);
        this.AdminUrls = ConfigLintList.Copy(AdminUrls);
        this.AdminRequiresAuthentication = AdminRequiresAuthentication;
        this.PublicMetricsEnabled = PublicMetricsEnabled;
        this.Listeners = ConfigLintList.Copy(Listeners);
        this.Routes = ConfigLintList.Copy(Routes);
    }

    public IReadOnlyList<string> SourceFiles { get; }

    public IReadOnlyList<string> AdminUrls { get; }

    public bool AdminRequiresAuthentication { get; }

    public bool PublicMetricsEnabled { get; }

    public IReadOnlyList<RuntimeListener> Listeners { get; }

    public IReadOnlyList<RuntimeRoute> Routes { get; }
}

public static class ProxyConfigLintRuntimeConfigurationSourceMapper
{
    public static ProxyConfigLintRuntimeConfigurationSource FromConfiguration(
        ProxyConfigurationSnapshot snapshot)
    {
        return new ProxyConfigLintRuntimeConfigurationSource(
            snapshot.SourceFiles,
            snapshot.AdminSecurity.Urls,
            snapshot.AdminSecurity.RequireAuthentication,
            snapshot.Metrics.PublicMetricsEnabled,
            snapshot.Listeners,
            snapshot.Routes);
    }
}

public static class ProxyConfigLintConfigurationSnapshotMapper
{
    public static ProxyConfigLintConfigurationSnapshot ToLintSnapshot(
        ProxyConfigLintRuntimeConfigurationSource source,
        RuntimeHttp3PlatformSupport platformSupport)
    {
        var http3 = Http3RuntimeSupport.ProjectConfiguration(
            Http3SupportSourceMapper.FromConfiguration(source.Listeners, source.Routes),
            platformSupport);
        return new ProxyConfigLintConfigurationSnapshot(
            source.SourceFiles,
            new ProxyConfigLintAdminSecurity(
                source.AdminUrls,
                source.AdminRequiresAuthentication),
            new ProxyConfigLintMetricsOptions(source.PublicMetricsEnabled),
            http3.QuicConnectionSupported,
            source.Listeners
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
            source.Routes
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
