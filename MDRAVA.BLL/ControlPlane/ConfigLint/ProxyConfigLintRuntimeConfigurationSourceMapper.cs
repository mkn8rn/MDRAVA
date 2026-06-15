using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Routing;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public sealed record ProxyConfigLintRuntimeConfigurationSource
{
    public ProxyConfigLintRuntimeConfigurationSource(
        IEnumerable<string> SourceFiles,
        IEnumerable<string> AdminUrls,
        bool AdminRequiresAuthentication,
        bool PublicMetricsEnabled,
        Http3SupportConfigurationSource Http3Support,
        IEnumerable<ProxyConfigLintRuntimeListenerSource> Listeners,
        IEnumerable<ProxyConfigLintRuntimeRouteSource> Routes)
    {
        ArgumentNullException.ThrowIfNull(SourceFiles);
        ArgumentNullException.ThrowIfNull(AdminUrls);
        ArgumentNullException.ThrowIfNull(Http3Support);
        ArgumentNullException.ThrowIfNull(Listeners);
        ArgumentNullException.ThrowIfNull(Routes);

        this.SourceFiles = ConfigLintList.Copy(SourceFiles);
        this.AdminUrls = ConfigLintList.Copy(AdminUrls);
        this.AdminRequiresAuthentication = AdminRequiresAuthentication;
        this.PublicMetricsEnabled = PublicMetricsEnabled;
        this.Http3Support = Http3Support;
        this.Listeners = ConfigLintList.Copy(Listeners);
        this.Routes = ConfigLintList.Copy(Routes);
    }

    public IReadOnlyList<string> SourceFiles { get; }

    public IReadOnlyList<string> AdminUrls { get; }

    public bool AdminRequiresAuthentication { get; }

    public bool PublicMetricsEnabled { get; }

    public Http3SupportConfigurationSource Http3Support { get; }

    public IReadOnlyList<ProxyConfigLintRuntimeListenerSource> Listeners { get; }

    public IReadOnlyList<ProxyConfigLintRuntimeRouteSource> Routes { get; }

}

public sealed record ProxyConfigLintRuntimeListenerSource(
    string Name,
    string Address,
    int Port,
    bool Enabled,
    string Transport,
    bool Http3Configured,
    bool Http3EnabledForTraffic,
    string Http3DisabledReason,
    string Http3EnablementLevel,
    bool Http3AltSvcEnabled,
    string? QuicIdentityKey);

public sealed record ProxyConfigLintRuntimeRouteSource
{
    public ProxyConfigLintRuntimeRouteSource(
        string Name,
        string SiteName,
        string Host,
        string PathPrefix,
        string Action,
        bool HttpsRedirectEnabled,
        bool CanonicalHostEnabled,
        string CanonicalHostTargetHost,
        bool CacheEnabled,
        IEnumerable<string> CacheVaryByHeaders,
        bool RetryEnabled,
        IEnumerable<string> RetryMethods,
        bool HealthCheckEnabled,
        IEnumerable<ProxyConfigLintRuntimeUpstreamSource> Upstreams,
        string StaticResponseBody)
    {
        ArgumentNullException.ThrowIfNull(CacheVaryByHeaders);
        ArgumentNullException.ThrowIfNull(RetryMethods);
        ArgumentNullException.ThrowIfNull(Upstreams);

        this.Name = Name;
        this.SiteName = SiteName;
        this.Host = Host;
        this.PathPrefix = PathPrefix;
        this.Action = Action;
        this.HttpsRedirectEnabled = HttpsRedirectEnabled;
        this.CanonicalHostEnabled = CanonicalHostEnabled;
        this.CanonicalHostTargetHost = CanonicalHostTargetHost;
        this.CacheEnabled = CacheEnabled;
        this.CacheVaryByHeaders = ConfigLintList.Copy(CacheVaryByHeaders);
        this.RetryEnabled = RetryEnabled;
        this.RetryMethods = ConfigLintList.Copy(RetryMethods);
        this.HealthCheckEnabled = HealthCheckEnabled;
        this.Upstreams = ConfigLintList.Copy(Upstreams);
        this.StaticResponseBody = StaticResponseBody;
    }

    public string Name { get; }

    public string SiteName { get; }

    public string Host { get; }

    public string PathPrefix { get; }

    public string Action { get; }

    public bool HttpsRedirectEnabled { get; }

    public bool CanonicalHostEnabled { get; }

    public string CanonicalHostTargetHost { get; }

    public bool CacheEnabled { get; }

    public IReadOnlyList<string> CacheVaryByHeaders { get; }

    public bool RetryEnabled { get; }

    public IReadOnlyList<string> RetryMethods { get; }

    public bool HealthCheckEnabled { get; }

    public IReadOnlyList<ProxyConfigLintRuntimeUpstreamSource> Upstreams { get; }

    public string StaticResponseBody { get; }

}

public sealed record ProxyConfigLintRuntimeUpstreamSource(
    string Name,
    string Scheme,
    string Protocol,
    bool TlsValidateCertificate,
    bool CircuitBreakerEnabled);

public static class ProxyConfigLintRuntimeConfigurationSourceMapper
{
    public static ProxyConfigLintRuntimeConfigurationSource FromConfiguration(
        IReadOnlyList<string> sourceFiles,
        IReadOnlyList<string> adminUrls,
        bool adminRequiresAuthentication,
        bool publicMetricsEnabled,
        IReadOnlyList<RuntimeListener> listeners,
        IReadOnlyList<RuntimeRoute> routes)
    {
        ArgumentNullException.ThrowIfNull(sourceFiles);
        ArgumentNullException.ThrowIfNull(adminUrls);
        ArgumentNullException.ThrowIfNull(listeners);
        ArgumentNullException.ThrowIfNull(routes);

        return new ProxyConfigLintRuntimeConfigurationSource(
            sourceFiles,
            adminUrls,
            adminRequiresAuthentication,
            publicMetricsEnabled,
            ProxyHttp3SupportConfigurationSourceMapper.FromConfiguration(
                listeners,
                routes),
            listeners
                .Select(static listener => new ProxyConfigLintRuntimeListenerSource(
                    listener.Name,
                    listener.Address,
                    listener.Port,
                    listener.Enabled,
                    RuntimeListenerTransportText.FromTransport(listener.Transport),
                    listener.Http3.Configured,
                    listener.Http3.EnabledForTraffic,
                    listener.Http3.DisabledReason,
                    listener.Http3.EnablementLevel,
                    Http3AltSvcListenerPolicy.IsEnabled(new Http3AltSvcListenerInput(
                        listener.Http3.EnabledForTraffic,
                        listener.Http3.EnablementLevel,
                        listener.Http3AltSvc.Enabled,
                        listener.Http3AltSvc.MaxAgeSeconds,
                        listener.Port,
                        listener.QuicIdentity?.Key)),
                    listener.QuicIdentity?.Key)),
            routes
                .Select(static route => new ProxyConfigLintRuntimeRouteSource(
                    route.Name,
                    route.SiteName,
                    route.Host,
                    route.PathPrefix,
                    ProxyRouteActionKindMapper.FromRuntimeActionText(route.Action),
                    route.HttpsRedirect.Enabled,
                    route.CanonicalHost.Enabled,
                    route.CanonicalHost.TargetHost,
                    route.Cache.Enabled,
                    route.Cache.VaryByHeaders,
                    route.Retry.Enabled,
                    route.Retry.RetryMethods,
                    route.HealthCheck.Enabled,
                    route.Upstreams
                        .Select(static upstream => new ProxyConfigLintRuntimeUpstreamSource(
                            upstream.Name,
                            upstream.Scheme,
                            upstream.Protocol,
                            upstream.Tls.ValidateCertificate,
                            upstream.CircuitBreaker.Enabled)),
                    route.StaticResponse.Body))
                );
    }
}

public static class ProxyConfigLintConfigurationSnapshotMapper
{
    public static ProxyConfigLintConfigurationSnapshot ToLintSnapshot(
        ProxyConfigLintRuntimeConfigurationSource source,
        RuntimeHttp3PlatformSupport platformSupport)
    {
        var http3 = Http3RuntimeSupport.ProjectConfiguration(source.Http3Support, platformSupport);
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
                    listener.Transport,
                    listener.Http3Configured,
                    listener.Http3EnabledForTraffic,
                    listener.Http3DisabledReason,
                    listener.Http3EnablementLevel,
                    listener.Http3AltSvcEnabled,
                    listener.QuicIdentityKey)),
            source.Routes
                .Select(static route => new ProxyConfigLintRoute(
                    route.Name,
                    route.SiteName,
                    route.Host,
                    route.PathPrefix,
                    route.Action,
                    route.HttpsRedirectEnabled,
                    route.CanonicalHostEnabled,
                    route.CanonicalHostTargetHost,
                    route.CacheEnabled,
                    route.CacheVaryByHeaders,
                    route.RetryEnabled,
                    route.RetryMethods,
                    route.HealthCheckEnabled,
                    route.Upstreams
                        .Select(static upstream => new ProxyConfigLintUpstream(
                            upstream.Name,
                            upstream.Scheme,
                            upstream.Protocol,
                            upstream.TlsValidateCertificate,
                            upstream.CircuitBreakerEnabled)),
                    route.StaticResponseBody))
                );
    }
}
