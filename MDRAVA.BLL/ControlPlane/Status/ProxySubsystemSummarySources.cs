using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.Listeners;

namespace MDRAVA.BLL.ControlPlane.Status;

public sealed record ProxyConfiguredListenerSummarySource(
    bool Enabled,
    bool Http1Enabled,
    bool Http2Enabled,
    bool Http3EnabledForTraffic);

public sealed record ProxyRuntimeListenerSummarySource(
    bool IsQuic,
    ProxyListenerState State);

public sealed record ProxyRouteSummarySource
{
    private string _siteName = string.Empty;

    public ProxyRouteSummarySource(
        string SiteName,
        bool IsProxyRoute,
        bool CacheEnabled,
        bool HasHttp3Upstream)
    {
        this.SiteName = SiteName;
        this.IsProxyRoute = IsProxyRoute;
        this.CacheEnabled = CacheEnabled;
        this.HasHttp3Upstream = HasHttp3Upstream;
    }

    public string SiteName
    {
        get => _siteName;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _siteName = value;
        }
    }

    public bool IsProxyRoute { get; init; }

    public bool CacheEnabled { get; init; }

    public bool HasHttp3Upstream { get; init; }
}

public sealed record ProxyCertificateSummarySource
{
    public ProxyCertificateSummarySource(
        IEnumerable<string> ReferencedCertificateIds,
        IEnumerable<ProxyCertificateValiditySource> LoadedCertificates)
    {
        ArgumentNullException.ThrowIfNull(ReferencedCertificateIds);
        ArgumentNullException.ThrowIfNull(LoadedCertificates);

        this.ReferencedCertificateIds = ProxyStatusList.CopyStrings(
            ReferencedCertificateIds,
            nameof(ReferencedCertificateIds));
        this.LoadedCertificates = ProxyStatusList.Copy(LoadedCertificates);
    }

    public IReadOnlyList<string> ReferencedCertificateIds { get; }

    public IReadOnlyList<ProxyCertificateValiditySource> LoadedCertificates { get; }
}

public sealed record ProxyCertificateValiditySource
{
    private string _id = string.Empty;

    public ProxyCertificateValiditySource(
        string Id,
        DateTime NotBefore,
        DateTime NotAfter)
    {
        this.Id = Id;
        this.NotBefore = NotBefore;
        this.NotAfter = NotAfter;
    }

    public string Id
    {
        get => _id;
        init
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            _id = value;
        }
    }

    public DateTime NotBefore { get; init; }

    public DateTime NotAfter { get; init; }
}

public sealed record ProxyAcmeSummaryConfigurationSource
{
    public ProxyAcmeSummaryConfigurationSource(
        bool Enabled,
        int ConfiguredCertificates)
    {
        ProxyStatusFacts.RequireNonNegative(ConfiguredCertificates, nameof(ConfiguredCertificates));

        this.Enabled = Enabled;
        this.ConfiguredCertificates = ConfiguredCertificates;
    }

    public bool Enabled { get; }

    public int ConfiguredCertificates { get; }
}

public sealed record ProxyUpstreamSummarySource(
    UpstreamHealthState HealthState,
    bool HealthCheckEnabled,
    bool CircuitBreakerEnabled,
    CircuitBreakerRuntimeState CircuitBreakerState);

public sealed record ProxyLimitConfigurationSummarySource
{
    public ProxyLimitConfigurationSummarySource(
        int MaxActiveClientConnections,
        int MaxConcurrentTlsHandshakes,
        int RequestsPerMinutePerIp)
    {
        ProxyStatusFacts.RequireNonNegative(MaxActiveClientConnections, nameof(MaxActiveClientConnections));
        ProxyStatusFacts.RequireNonNegative(MaxConcurrentTlsHandshakes, nameof(MaxConcurrentTlsHandshakes));
        ProxyStatusFacts.RequireNonNegative(RequestsPerMinutePerIp, nameof(RequestsPerMinutePerIp));

        this.MaxActiveClientConnections = MaxActiveClientConnections;
        this.MaxConcurrentTlsHandshakes = MaxConcurrentTlsHandshakes;
        this.RequestsPerMinutePerIp = RequestsPerMinutePerIp;
    }

    public int MaxActiveClientConnections { get; }

    public int MaxConcurrentTlsHandshakes { get; }

    public int RequestsPerMinutePerIp { get; }
}

public sealed record ProxyLimitRuntimeSummarySource
{
    public ProxyLimitRuntimeSummarySource(
        long ActiveConnections,
        long ActiveTlsHandshakes,
        long ActiveHttp2Streams,
        long ActiveHttp3Streams,
        long ActiveUpstreamHttp3Streams)
    {
        ProxyStatusFacts.RequireNonNegative(ActiveConnections, nameof(ActiveConnections));
        ProxyStatusFacts.RequireNonNegative(ActiveTlsHandshakes, nameof(ActiveTlsHandshakes));
        ProxyStatusFacts.RequireNonNegative(ActiveHttp2Streams, nameof(ActiveHttp2Streams));
        ProxyStatusFacts.RequireNonNegative(ActiveHttp3Streams, nameof(ActiveHttp3Streams));
        ProxyStatusFacts.RequireNonNegative(ActiveUpstreamHttp3Streams, nameof(ActiveUpstreamHttp3Streams));

        this.ActiveConnections = ActiveConnections;
        this.ActiveTlsHandshakes = ActiveTlsHandshakes;
        this.ActiveHttp2Streams = ActiveHttp2Streams;
        this.ActiveHttp3Streams = ActiveHttp3Streams;
        this.ActiveUpstreamHttp3Streams = ActiveUpstreamHttp3Streams;
    }

    public long ActiveConnections { get; }

    public long ActiveTlsHandshakes { get; }

    public long ActiveHttp2Streams { get; }

    public long ActiveHttp3Streams { get; }

    public long ActiveUpstreamHttp3Streams { get; }
}

public sealed record ProxyLogSummarySource
{
    public ProxyLogSummarySource(
        bool AccessLogPersistenceEnabled,
        bool AdminAuditPersistenceEnabled,
        string State,
        string Reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(State);
        ArgumentException.ThrowIfNullOrWhiteSpace(Reason);

        this.AccessLogPersistenceEnabled = AccessLogPersistenceEnabled;
        this.AdminAuditPersistenceEnabled = AdminAuditPersistenceEnabled;
        this.State = State;
        this.Reason = Reason;
    }

    public bool AccessLogPersistenceEnabled { get; }

    public bool AdminAuditPersistenceEnabled { get; }

    public string State { get; }

    public string Reason { get; }
}

public sealed record ProxyShutdownSummarySource
{
    public ProxyShutdownSummarySource(
        bool IsRunning,
        bool IsShuttingDown,
        DateTimeOffset? ShutdownStartedAtUtc,
        DateTimeOffset? ShutdownDeadlineUtc)
    {
        ProxyStatusFacts.RequireShutdownWindow(
            IsShuttingDown,
            ShutdownStartedAtUtc,
            nameof(ShutdownStartedAtUtc),
            ShutdownDeadlineUtc,
            nameof(ShutdownDeadlineUtc));

        this.IsRunning = IsRunning;
        this.IsShuttingDown = IsShuttingDown;
        this.ShutdownStartedAtUtc = ShutdownStartedAtUtc;
        this.ShutdownDeadlineUtc = ShutdownDeadlineUtc;
    }

    public bool IsRunning { get; }

    public bool IsShuttingDown { get; }

    public DateTimeOffset? ShutdownStartedAtUtc { get; }

    public DateTimeOffset? ShutdownDeadlineUtc { get; }
}
