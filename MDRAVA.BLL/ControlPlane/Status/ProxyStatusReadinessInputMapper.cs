using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Listeners;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.BLL.ControlPlane.Status;

public sealed record ProxyStatusReadinessSourceSet
{
    public ProxyStatusReadinessSourceSet(
        bool HasActiveConfiguration,
        int? ConfigGeneration,
        DateTimeOffset? ConfigurationLoadedAtUtc,
        bool? LastListenerReloadSucceeded,
        bool LastListenerReloadFailed,
        IReadOnlyList<ProxyConfiguredListenerSummarySource> ConfiguredListeners,
        IReadOnlyList<ProxyRuntimeListenerSummarySource> RuntimeListeners,
        IReadOnlyList<ProxyRouteSummarySource> Routes,
        ProxyCertificateSummarySource? Certificates,
        ProxyAcmeSummaryConfigurationSource? Acme,
        IReadOnlyList<ProxyUpstreamSummarySource> Upstreams,
        ProxyLimitConfigurationSummarySource? LimitConfiguration,
        ProxyLimitRuntimeSummarySource LimitRuntime,
        bool ClientHttp3Enabled,
        bool ClientHttp3Ready,
        ProxyLogSummarySource Log,
        ProxyShutdownSummarySource Shutdown)
    {
        ArgumentNullException.ThrowIfNull(ConfiguredListeners);
        ArgumentNullException.ThrowIfNull(RuntimeListeners);
        ArgumentNullException.ThrowIfNull(Routes);
        ArgumentNullException.ThrowIfNull(Upstreams);
        ProxyStatusFacts.RequireOptionalNonNegative(ConfigGeneration, nameof(ConfigGeneration));

        this.HasActiveConfiguration = HasActiveConfiguration;
        this.ConfigGeneration = ConfigGeneration;
        this.ConfigurationLoadedAtUtc = ConfigurationLoadedAtUtc;
        this.LastListenerReloadSucceeded = LastListenerReloadSucceeded;
        this.LastListenerReloadFailed = LastListenerReloadFailed;
        this.ConfiguredListeners = ProxyStatusList.Copy(ConfiguredListeners);
        this.RuntimeListeners = ProxyStatusList.Copy(RuntimeListeners);
        this.Routes = ProxyStatusList.Copy(Routes);
        this.Certificates = Certificates;
        this.Acme = Acme;
        this.Upstreams = ProxyStatusList.Copy(Upstreams);
        this.LimitConfiguration = LimitConfiguration;
        this.LimitRuntime = LimitRuntime;
        this.ClientHttp3Enabled = ClientHttp3Enabled;
        this.ClientHttp3Ready = ClientHttp3Ready;
        this.Log = Log;
        this.Shutdown = Shutdown;
    }

    public bool HasActiveConfiguration { get; }

    public int? ConfigGeneration { get; }

    public DateTimeOffset? ConfigurationLoadedAtUtc { get; }

    public bool? LastListenerReloadSucceeded { get; }

    public bool LastListenerReloadFailed { get; }

    public IReadOnlyList<ProxyConfiguredListenerSummarySource> ConfiguredListeners { get; }

    public IReadOnlyList<ProxyRuntimeListenerSummarySource> RuntimeListeners { get; }

    public IReadOnlyList<ProxyRouteSummarySource> Routes { get; }

    public ProxyCertificateSummarySource? Certificates { get; }

    public ProxyAcmeSummaryConfigurationSource? Acme { get; }

    public IReadOnlyList<ProxyUpstreamSummarySource> Upstreams { get; }

    public ProxyLimitConfigurationSummarySource? LimitConfiguration { get; }

    public ProxyLimitRuntimeSummarySource LimitRuntime { get; }

    public bool ClientHttp3Enabled { get; }

    public bool ClientHttp3Ready { get; }

    public ProxyLogSummarySource Log { get; }

    public ProxyShutdownSummarySource Shutdown { get; }
}

public sealed record ProxyStatusReadinessConfigurationSourceSet
{
    public ProxyStatusReadinessConfigurationSourceSet(
        bool HasActiveConfiguration,
        int? ConfigGeneration,
        DateTimeOffset? ConfigurationLoadedAtUtc,
        IReadOnlyList<ProxyConfiguredListenerSummarySource> ConfiguredListeners,
        IReadOnlyList<ProxyRouteSummarySource> Routes,
        ProxyCertificateSummarySource? Certificates,
        ProxyAcmeSummaryConfigurationSource? Acme,
        ProxyLimitConfigurationSummarySource? LimitConfiguration)
    {
        ArgumentNullException.ThrowIfNull(ConfiguredListeners);
        ArgumentNullException.ThrowIfNull(Routes);
        ProxyStatusFacts.RequireOptionalNonNegative(ConfigGeneration, nameof(ConfigGeneration));

        this.HasActiveConfiguration = HasActiveConfiguration;
        this.ConfigGeneration = ConfigGeneration;
        this.ConfigurationLoadedAtUtc = ConfigurationLoadedAtUtc;
        this.ConfiguredListeners = ProxyStatusList.Copy(ConfiguredListeners);
        this.Routes = ProxyStatusList.Copy(Routes);
        this.Certificates = Certificates;
        this.Acme = Acme;
        this.LimitConfiguration = LimitConfiguration;
    }

    public static ProxyStatusReadinessConfigurationSourceSet Missing { get; } = new(
        HasActiveConfiguration: false,
        ConfigGeneration: null,
        ConfigurationLoadedAtUtc: null,
        ConfiguredListeners: [],
        Routes: [],
        Certificates: null,
        Acme: null,
        LimitConfiguration: null);

    public bool HasActiveConfiguration { get; }

    public int? ConfigGeneration { get; }

    public DateTimeOffset? ConfigurationLoadedAtUtc { get; }

    public IReadOnlyList<ProxyConfiguredListenerSummarySource> ConfiguredListeners { get; }

    public IReadOnlyList<ProxyRouteSummarySource> Routes { get; }

    public ProxyCertificateSummarySource? Certificates { get; }

    public ProxyAcmeSummaryConfigurationSource? Acme { get; }

    public ProxyLimitConfigurationSummarySource? LimitConfiguration { get; }
}

public static class ProxyStatusReadinessSourceMapper
{
    public static ProxyStatusReadinessSourceSet FromSources(
        ProxyStatusReadinessConfigurationSourceSet configuration,
        ProxyStatusRuntimeSummary runtime,
        ProxyMetricsSnapshot metrics,
        IReadOnlyList<ProxyUpstreamStatus> upstreams,
        RuntimeHttp3SupportProjection http3,
        ProxyLogPersistenceStatus logPersistence)
    {
        return new ProxyStatusReadinessSourceSet(
            configuration.HasActiveConfiguration,
            configuration.ConfigGeneration,
            configuration.ConfigurationLoadedAtUtc,
            runtime.LastListenerReload is ProxyListenerReloadResult.AppliedResult,
            runtime.LastListenerReload is ProxyListenerReloadResult.FailedResult,
            configuration.ConfiguredListeners,
            ProxyRuntimeListenerSummarySourceMapper.FromSources(runtime.Listeners),
            configuration.Routes,
            configuration.Certificates,
            configuration.Acme,
            ProxyUpstreamSummarySourceMapper.FromStatusResponses(upstreams),
            configuration.LimitConfiguration,
            ProxyLimitSummarySourceMapper.FromSources(
                metrics.ClientConnections.Active,
                metrics.Tls.ActiveHandshakes,
                metrics.Http2.ActiveStreams,
                metrics.Http3.ActiveStreams,
                metrics.UpstreamHttp3.ActiveStreams),
            http3.EnabledForTraffic,
            http3.QuicListenerReady,
            ProxyLogSummarySourceMapper.FromStatus(logPersistence),
            ProxyShutdownSummarySourceMapper.FromSources(
                runtime.ListenerLive,
                runtime.IsShuttingDown,
                runtime.ShutdownStartedAtUtc,
                runtime.ShutdownDeadlineUtc));
    }
}

public static class ProxyStatusReadinessInputMapper
{
    public static ProxyStatusReadinessInput FromSources(
        ProxyStatusReadinessSourceSet sources,
        ProxyCacheStatus? cacheStatus,
        IReadOnlyList<AcmeCertificateLifecycleStatus> acmeStatuses,
        ProxyRuntimePreflightStatus runtimePreflight,
        DateTimeOffset observedAtUtc)
    {
        return new ProxyStatusReadinessInput(
            sources.HasActiveConfiguration,
            sources.ConfigGeneration,
            sources.ConfigurationLoadedAtUtc,
            sources.LastListenerReloadSucceeded,
            sources.LastListenerReloadFailed,
            sources.ConfiguredListeners,
            sources.RuntimeListeners,
            sources.Routes,
            sources.Certificates,
            sources.Acme,
            sources.Upstreams,
            sources.LimitConfiguration,
            sources.LimitRuntime,
            sources.ClientHttp3Enabled,
            sources.ClientHttp3Ready,
            sources.Log,
            sources.Shutdown,
            cacheStatus,
            acmeStatuses,
            runtimePreflight,
            observedAtUtc);
    }
}
