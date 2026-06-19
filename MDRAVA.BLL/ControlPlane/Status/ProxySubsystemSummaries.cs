namespace MDRAVA.BLL.ControlPlane.Status;

public sealed record ProxySubsystemSummaries(
    ProxyConfigSubsystemSummary Config,
    ProxyListenerSubsystemSummary Listeners,
    ProxyRouteSubsystemSummary Routes,
    ProxyCertificateSubsystemSummary Certificates,
    ProxyAcmeSubsystemSummary Acme,
    ProxyUpstreamSubsystemSummary Upstreams,
    ProxyCacheSubsystemSummary Cache,
    ProxyCircuitSubsystemSummary Circuits,
    ProxyLimitSubsystemSummary Limits,
    ProxyLogSubsystemSummary Logs,
    ProxyShutdownSubsystemSummary Shutdown,
    ProxyProtocolSubsystemSummary Protocols)
{
    public static ProxySubsystemSummaries Unknown { get; } = new(
        ProxyConfigSubsystemSummary.Unknown,
        ProxyListenerSubsystemSummary.Unknown,
        ProxyRouteSubsystemSummary.Unknown,
        ProxyCertificateSubsystemSummary.Unknown,
        ProxyAcmeSubsystemSummary.Unknown,
        ProxyUpstreamSubsystemSummary.Unknown,
        ProxyCacheSubsystemSummary.Unknown,
        ProxyCircuitSubsystemSummary.Unknown,
        ProxyLimitSubsystemSummary.Unknown,
        ProxyLogSubsystemSummary.Unknown,
        ProxyShutdownSubsystemSummary.Unknown,
        ProxyProtocolSubsystemSummary.Unknown);
}

public sealed record ProxyConfigSubsystemSummary(
    bool Active,
    int? Generation,
    DateTimeOffset? LoadedAtUtc,
    bool? LastListenerReloadSucceeded,
    string? LastListenerReloadReason)
{
    public static ProxyConfigSubsystemSummary Unknown { get; } = new(false, null, null, null, ProxyStatusText.NotAvailable);
}

public sealed record ProxyListenerSubsystemSummary(
    int Configured,
    int Enabled,
    int Active,
    int Failed,
    int Draining,
    int Http1Enabled,
    int Http2Enabled,
    int Http3Enabled,
    int QuicReady)
{
    public static ProxyListenerSubsystemSummary Unknown { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0);
}

public sealed record ProxyRouteSubsystemSummary(
    int Sites,
    int Routes,
    int ProxyRoutes,
    int GeneratedRoutes,
    int CacheEnabledRoutes)
{
    public static ProxyRouteSubsystemSummary Unknown { get; } = new(0, 0, 0, 0, 0);
}

public sealed record ProxySubsystemIssueSummary(
    DateTimeOffset TimestampUtc,
    string Category,
    string Reason,
    string? AffectedIdentity);

public sealed record ProxyCertificateSubsystemSummary(
    int Configured,
    int Loaded,
    int MissingReferences,
    int Expired,
    int NotYetValid,
    int ExpiringSoon,
    ProxySubsystemIssueSummary? LastIssue)
{
    public static ProxyCertificateSubsystemSummary Unknown { get; } = new(0, 0, 0, 0, 0, 0, null);
}

public sealed record ProxyAcmeSubsystemSummary(
    bool Enabled,
    int Configured,
    int Active,
    int Failed,
    int RenewalBackoff,
    ProxySubsystemIssueSummary? LastIssue)
{
    public static ProxyAcmeSubsystemSummary Unknown { get; } = new(false, 0, 0, 0, 0, null);
}

public sealed record ProxyUpstreamSubsystemSummary(
    int Total,
    int Healthy,
    int Unhealthy,
    int UnknownHealth,
    int HealthChecksEnabled)
{
    public static ProxyUpstreamSubsystemSummary Unknown { get; } = new(0, 0, 0, 0, 0);
}

public sealed record ProxyCacheSubsystemSummary
{
    public ProxyCacheSubsystemSummary(
        bool Enabled,
        int EnabledRoutes,
        int EntryCount,
        long ApproximateBytes)
    {
        ProxyStatusFacts.RequireNonNegative(EnabledRoutes, nameof(EnabledRoutes));
        ProxyStatusFacts.RequireNonNegative(EntryCount, nameof(EntryCount));
        ProxyStatusFacts.RequireNonNegative(ApproximateBytes, nameof(ApproximateBytes));

        this.Enabled = Enabled;
        this.EnabledRoutes = EnabledRoutes;
        this.EntryCount = EntryCount;
        this.ApproximateBytes = ApproximateBytes;
    }

    public bool Enabled { get; }

    public int EnabledRoutes { get; }

    public int EntryCount { get; }

    public long ApproximateBytes { get; }

    public static ProxyCacheSubsystemSummary Unknown { get; } = new(false, 0, 0, 0);
}

public sealed record ProxyCircuitSubsystemSummary(
    int Enabled,
    int Open,
    int HalfOpen,
    int Closed)
{
    public static ProxyCircuitSubsystemSummary Unknown { get; } = new(0, 0, 0, 0);
}

public sealed record ProxyLimitSubsystemSummary
{
    public ProxyLimitSubsystemSummary(
        int MaxActiveClientConnections,
        long ActiveConnections,
        int MaxConcurrentTlsHandshakes,
        long ActiveTlsHandshakes,
        long ActiveHttp2Streams,
        long ActiveHttp3Streams,
        long ActiveUpstreamHttp3Streams,
        int RequestsPerMinutePerIp)
    {
        ProxyStatusFacts.RequireNonNegative(MaxActiveClientConnections, nameof(MaxActiveClientConnections));
        ProxyStatusFacts.RequireNonNegative(ActiveConnections, nameof(ActiveConnections));
        ProxyStatusFacts.RequireNonNegative(MaxConcurrentTlsHandshakes, nameof(MaxConcurrentTlsHandshakes));
        ProxyStatusFacts.RequireNonNegative(ActiveTlsHandshakes, nameof(ActiveTlsHandshakes));
        ProxyStatusFacts.RequireNonNegative(ActiveHttp2Streams, nameof(ActiveHttp2Streams));
        ProxyStatusFacts.RequireNonNegative(ActiveHttp3Streams, nameof(ActiveHttp3Streams));
        ProxyStatusFacts.RequireNonNegative(ActiveUpstreamHttp3Streams, nameof(ActiveUpstreamHttp3Streams));
        ProxyStatusFacts.RequireNonNegative(RequestsPerMinutePerIp, nameof(RequestsPerMinutePerIp));

        this.MaxActiveClientConnections = MaxActiveClientConnections;
        this.ActiveConnections = ActiveConnections;
        this.MaxConcurrentTlsHandshakes = MaxConcurrentTlsHandshakes;
        this.ActiveTlsHandshakes = ActiveTlsHandshakes;
        this.ActiveHttp2Streams = ActiveHttp2Streams;
        this.ActiveHttp3Streams = ActiveHttp3Streams;
        this.ActiveUpstreamHttp3Streams = ActiveUpstreamHttp3Streams;
        this.RequestsPerMinutePerIp = RequestsPerMinutePerIp;
    }

    public int MaxActiveClientConnections { get; }

    public long ActiveConnections { get; }

    public int MaxConcurrentTlsHandshakes { get; }

    public long ActiveTlsHandshakes { get; }

    public long ActiveHttp2Streams { get; }

    public long ActiveHttp3Streams { get; }

    public long ActiveUpstreamHttp3Streams { get; }

    public int RequestsPerMinutePerIp { get; }

    public static ProxyLimitSubsystemSummary Unknown { get; } = new(0, 0, 0, 0, 0, 0, 0, 0);
}

public sealed record ProxyLogSubsystemSummary(
    bool AccessLogPersistenceEnabled,
    bool AdminAuditPersistenceEnabled,
    string State,
    string Reason)
{
    public static ProxyLogSubsystemSummary Unknown { get; } = new(false, false, ProxyStatusText.Unknown, ProxyStatusText.NotAvailable);
}

public sealed record ProxyShutdownSubsystemSummary(
    bool IsRunning,
    bool IsShuttingDown,
    DateTimeOffset? ShutdownStartedAtUtc,
    DateTimeOffset? ShutdownDeadlineUtc)
{
    public static ProxyShutdownSubsystemSummary Unknown { get; } = new(false, false, null, null);
}

public sealed record ProxyProtocolSubsystemSummary
{
    public ProxyProtocolSubsystemSummary(
        bool clientHttp1Enabled,
        bool clientHttp2Enabled,
        bool clientHttp3Enabled,
        bool clientHttp3Ready,
        bool upstreamHttp3Configured,
        IReadOnlyList<string> unsupportedHttp3Features)
    {
        ClientHttp1Enabled = clientHttp1Enabled;
        ClientHttp2Enabled = clientHttp2Enabled;
        ClientHttp3Enabled = clientHttp3Enabled;
        ClientHttp3Ready = clientHttp3Ready;
        UpstreamHttp3Configured = upstreamHttp3Configured;
        UnsupportedHttp3Features = ProxyStatusList.Copy(unsupportedHttp3Features);
    }

    public bool ClientHttp1Enabled { get; }

    public bool ClientHttp2Enabled { get; }

    public bool ClientHttp3Enabled { get; }

    public bool ClientHttp3Ready { get; }

    public bool UpstreamHttp3Configured { get; }

    public IReadOnlyList<string> UnsupportedHttp3Features { get; }

    public static ProxyProtocolSubsystemSummary Unknown { get; } = new(
        false,
        false,
        false,
        false,
        false,
        []);
}
