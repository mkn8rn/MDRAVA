namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public sealed record ProxyConfigLintConfigurationSnapshot
{
    public ProxyConfigLintConfigurationSnapshot(
        IEnumerable<string> SourceFiles,
        ProxyConfigLintAdminSecurity AdminSecurity,
        ProxyConfigLintMetricsOptions Metrics,
        bool Http3QuicConnectionSupported,
        IEnumerable<ProxyConfigLintListener> Listeners,
        IEnumerable<ProxyConfigLintRoute> Routes)
    {
        ArgumentNullException.ThrowIfNull(SourceFiles);
        ArgumentNullException.ThrowIfNull(AdminSecurity);
        ArgumentNullException.ThrowIfNull(Metrics);
        ArgumentNullException.ThrowIfNull(Listeners);
        ArgumentNullException.ThrowIfNull(Routes);

        this.SourceFiles = ConfigLintList.Copy(SourceFiles);
        this.AdminSecurity = AdminSecurity;
        this.Metrics = Metrics;
        this.Http3QuicConnectionSupported = Http3QuicConnectionSupported;
        this.Listeners = ConfigLintList.Copy(Listeners);
        this.Routes = ConfigLintList.Copy(Routes);
    }

    public IReadOnlyList<string> SourceFiles { get; }

    public ProxyConfigLintAdminSecurity AdminSecurity { get; }

    public ProxyConfigLintMetricsOptions Metrics { get; }

    public bool Http3QuicConnectionSupported { get; }

    public IReadOnlyList<ProxyConfigLintListener> Listeners { get; }

    public IReadOnlyList<ProxyConfigLintRoute> Routes { get; }
}

public sealed record ProxyConfigLintAdminSecurity
{
    public ProxyConfigLintAdminSecurity(
        IEnumerable<string> Urls,
        bool RequireAuthentication)
    {
        ArgumentNullException.ThrowIfNull(Urls);

        this.Urls = ConfigLintList.Copy(Urls);
        this.RequireAuthentication = RequireAuthentication;
    }

    public IReadOnlyList<string> Urls { get; }

    public bool RequireAuthentication { get; }
}

public sealed record ProxyConfigLintMetricsOptions(
    bool PublicMetricsEnabled);

public sealed record ProxyConfigLintListener
{
    public ProxyConfigLintListener(
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

public sealed record ProxyConfigLintRoute
{
    public ProxyConfigLintRoute(
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
        IEnumerable<ProxyConfigLintUpstream> Upstreams,
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

    public IReadOnlyList<ProxyConfigLintUpstream> Upstreams { get; }

    public string StaticResponseBody { get; }
}

public sealed record ProxyConfigLintUpstream
{
    public ProxyConfigLintUpstream(
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
