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
        this.Listeners = ConfigLintList.Copy(Listeners.Select(RequireListenerSource));
        this.Routes = ConfigLintList.Copy(Routes.Select(RequireRouteSource));
    }

    public IReadOnlyList<string> SourceFiles { get; }

    public IReadOnlyList<string> AdminUrls { get; }

    public bool AdminRequiresAuthentication { get; }

    public bool PublicMetricsEnabled { get; }

    public Http3SupportConfigurationSource Http3Support { get; }

    public IReadOnlyList<ProxyConfigLintRuntimeListenerSource> Listeners { get; }

    public IReadOnlyList<ProxyConfigLintRuntimeRouteSource> Routes { get; }

    private static ProxyConfigLintRuntimeListenerSource RequireListenerSource(
        ProxyConfigLintRuntimeListenerSource listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        return listener;
    }

    private static ProxyConfigLintRuntimeRouteSource RequireRouteSource(
        ProxyConfigLintRuntimeRouteSource route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return route;
    }
}

public sealed record ProxyConfigLintRuntimeListenerSource
{
    public ProxyConfigLintRuntimeListenerSource(
        string name,
        string address,
        int port,
        bool enabled,
        string transport,
        bool http3Configured,
        bool http3EnabledForTraffic,
        string http3DisabledReason,
        string http3EnablementLevel,
        bool http3AltSvcEnabled,
        string? quicIdentityKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(address);
        ArgumentOutOfRangeException.ThrowIfNegative(port);
        ArgumentException.ThrowIfNullOrWhiteSpace(transport);
        ArgumentNullException.ThrowIfNull(http3DisabledReason);
        ArgumentException.ThrowIfNullOrWhiteSpace(http3EnablementLevel);

        Name = name;
        Address = address;
        Port = port;
        Enabled = enabled;
        Transport = transport;
        Http3Configured = http3Configured;
        Http3EnabledForTraffic = http3EnabledForTraffic;
        Http3DisabledReason = http3DisabledReason;
        Http3EnablementLevel = http3EnablementLevel;
        Http3AltSvcEnabled = http3AltSvcEnabled;
        QuicIdentityKey = quicIdentityKey;
    }

    public string Name { get; }

    public string Address { get; }

    public int Port { get; }

    public bool Enabled { get; }

    public string Transport { get; }

    public bool Http3Configured { get; }

    public bool Http3EnabledForTraffic { get; }

    public string Http3DisabledReason { get; }

    public string Http3EnablementLevel { get; }

    public bool Http3AltSvcEnabled { get; }

    public string? QuicIdentityKey { get; }
}

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
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        ArgumentException.ThrowIfNullOrWhiteSpace(SiteName);
        ArgumentNullException.ThrowIfNull(Host);
        ArgumentException.ThrowIfNullOrWhiteSpace(PathPrefix);
        ArgumentException.ThrowIfNullOrWhiteSpace(Action);
        ArgumentNullException.ThrowIfNull(CanonicalHostTargetHost);
        ArgumentNullException.ThrowIfNull(CacheVaryByHeaders);
        ArgumentNullException.ThrowIfNull(RetryMethods);
        ArgumentNullException.ThrowIfNull(Upstreams);
        ArgumentNullException.ThrowIfNull(StaticResponseBody);

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
        this.Upstreams = ConfigLintList.Copy(Upstreams.Select(RequireUpstreamSource));
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

    private static ProxyConfigLintRuntimeUpstreamSource RequireUpstreamSource(
        ProxyConfigLintRuntimeUpstreamSource upstream)
    {
        ArgumentNullException.ThrowIfNull(upstream);

        return upstream;
    }
}

public sealed record ProxyConfigLintRuntimeUpstreamSource
{
    public ProxyConfigLintRuntimeUpstreamSource(
        string name,
        string scheme,
        string protocol,
        bool tlsValidateCertificate,
        bool circuitBreakerEnabled)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(scheme);
        ArgumentException.ThrowIfNullOrWhiteSpace(protocol);

        Name = name;
        Scheme = scheme;
        Protocol = protocol;
        TlsValidateCertificate = tlsValidateCertificate;
        CircuitBreakerEnabled = circuitBreakerEnabled;
    }

    public string Name { get; }

    public string Scheme { get; }

    public string Protocol { get; }

    public bool TlsValidateCertificate { get; }

    public bool CircuitBreakerEnabled { get; }
}

public static class ProxyConfigLintRuntimeConfigurationSourceMapper
{
    public static ProxyConfigLintRuntimeConfigurationSource FromSources(
        IEnumerable<string> sourceFiles,
        IEnumerable<string> adminUrls,
        bool adminRequiresAuthentication,
        bool publicMetricsEnabled,
        IEnumerable<RuntimeListener> listeners,
        IEnumerable<RuntimeRoute> routes)
    {
        ArgumentNullException.ThrowIfNull(sourceFiles);
        ArgumentNullException.ThrowIfNull(adminUrls);
        ArgumentNullException.ThrowIfNull(listeners);
        ArgumentNullException.ThrowIfNull(routes);

        var listenerSources = ConfigLintList.Copy(listeners);
        var routeSources = ConfigLintList.Copy(routes);

        return new ProxyConfigLintRuntimeConfigurationSource(
            sourceFiles,
            adminUrls,
            adminRequiresAuthentication,
            publicMetricsEnabled,
            ProxyHttp3SupportConfigurationSourceMapper.FromSources(
                listenerSources,
                routeSources),
            listenerSources.Select(ToListenerSource),
            routeSources.Select(ToRouteSource)
                );
    }

    private static ProxyConfigLintRuntimeListenerSource ToListenerSource(RuntimeListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        return new ProxyConfigLintRuntimeListenerSource(
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
            listener.QuicIdentity?.Key);
    }

    private static ProxyConfigLintRuntimeRouteSource ToRouteSource(RuntimeRoute route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return new ProxyConfigLintRuntimeRouteSource(
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
            route.Upstreams.Select(ToUpstreamSource),
            route.StaticResponse.Body);
    }

    private static ProxyConfigLintRuntimeUpstreamSource ToUpstreamSource(RuntimeUpstream upstream)
    {
        ArgumentNullException.ThrowIfNull(upstream);

        return new ProxyConfigLintRuntimeUpstreamSource(
            upstream.Name,
            upstream.Scheme,
            upstream.Protocol,
            upstream.Tls.ValidateCertificate,
            upstream.CircuitBreaker.Enabled);
    }
}

public static class ProxyConfigLintConfigurationSnapshotMapper
{
    public static ProxyConfigLintConfigurationSnapshot ToLintSnapshot(
        ProxyConfigLintRuntimeConfigurationSource source,
        RuntimeHttp3PlatformSupport platformSupport)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(platformSupport);

        var http3 = Http3RuntimeSupport.ProjectConfiguration(source.Http3Support, platformSupport);
        return new ProxyConfigLintConfigurationSnapshot(
            source.SourceFiles,
            new ProxyConfigLintAdminSecurity(
                source.AdminUrls,
                source.AdminRequiresAuthentication),
            new ProxyConfigLintMetricsOptions(source.PublicMetricsEnabled),
            http3.QuicConnectionSupported,
            source.Listeners.Select(ToLintListener),
            source.Routes.Select(ToLintRoute)
                );
    }

    private static ProxyConfigLintListener ToLintListener(ProxyConfigLintRuntimeListenerSource listener)
    {
        ArgumentNullException.ThrowIfNull(listener);

        return new ProxyConfigLintListener(
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
            listener.QuicIdentityKey);
    }

    private static ProxyConfigLintRoute ToLintRoute(ProxyConfigLintRuntimeRouteSource route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return new ProxyConfigLintRoute(
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
            route.Upstreams.Select(ToLintUpstream),
            route.StaticResponseBody);
    }

    private static ProxyConfigLintUpstream ToLintUpstream(ProxyConfigLintRuntimeUpstreamSource upstream)
    {
        ArgumentNullException.ThrowIfNull(upstream);

        return new ProxyConfigLintUpstream(
            upstream.Name,
            upstream.Scheme,
            upstream.Protocol,
            upstream.TlsValidateCertificate,
            upstream.CircuitBreakerEnabled);
    }
}
