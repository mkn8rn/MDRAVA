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

public sealed record ProxyConfigLintListener(
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

    public IReadOnlyList<ProxyConfigLintUpstream> Upstreams { get; }

    public string StaticResponseBody { get; }
}

public sealed record ProxyConfigLintUpstream(
    string Name,
    string Scheme,
    string Protocol,
    bool TlsValidateCertificate,
    bool CircuitBreakerEnabled);
