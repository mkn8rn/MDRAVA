using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Listeners;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.BLL.ControlPlane.Status;

public sealed record ProxyStatusReadinessSourceSet(
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
    ProxyShutdownSummarySource Shutdown);

public sealed record ProxyStatusReadinessConfigurationSourceSet(
    bool HasActiveConfiguration,
    int? ConfigGeneration,
    DateTimeOffset? ConfigurationLoadedAtUtc,
    IReadOnlyList<ProxyConfiguredListenerSummarySource> ConfiguredListeners,
    IReadOnlyList<ProxyRouteSummarySource> Routes,
    ProxyCertificateSummarySource? Certificates,
    ProxyAcmeSummaryConfigurationSource? Acme,
    ProxyLimitConfigurationSummarySource? LimitConfiguration)
{
    public static ProxyStatusReadinessConfigurationSourceSet Missing { get; } = new(
        HasActiveConfiguration: false,
        ConfigGeneration: null,
        ConfigurationLoadedAtUtc: null,
        ConfiguredListeners: [],
        Routes: [],
        Certificates: null,
        Acme: null,
        LimitConfiguration: null);
}

public static class ProxyStatusReadinessConfigurationSourceMapper
{
    public static ProxyStatusReadinessConfigurationSourceSet FromConfiguration(
        ProxyConfigurationSnapshot? configuration)
    {
        if (configuration is null)
        {
            return ProxyStatusReadinessConfigurationSourceSet.Missing;
        }

        return new ProxyStatusReadinessConfigurationSourceSet(
            true,
            configuration.Version,
            configuration.LoadedAtUtc,
            ProxyConfiguredListenerSummarySourceMapper.FromListeners(configuration.Listeners),
            ProxyRouteSummarySourceMapper.FromRoutes(configuration.Routes),
            ProxyCertificateSummarySourceMapper.FromConfiguration(
                configuration.Listeners,
                configuration.Certificates),
            ProxyAcmeSummaryConfigurationSourceMapper.FromConfiguration(configuration.Acme),
            ProxyLimitSummarySourceMapper.FromConfiguration(configuration.Limits));
    }
}

public static class ProxyStatusReadinessSourceMapper
{
    public static ProxyStatusReadinessSourceSet FromSources(
        ProxyStatusReadinessConfigurationSourceSet configuration,
        ProxyStatusRuntimeSummary runtime,
        ProxyMetricsSnapshot metrics,
        IReadOnlyList<ProxyUpstreamStatusResponse> upstreams,
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
            ProxyRuntimeListenerSummarySourceMapper.FromRuntimeSummary(runtime),
            configuration.Routes,
            configuration.Certificates,
            configuration.Acme,
            ProxyUpstreamSummarySourceMapper.FromStatusResponses(upstreams),
            configuration.LimitConfiguration,
            ProxyLimitSummarySourceMapper.FromMetrics(metrics),
            http3.EnabledForTraffic,
            http3.QuicListenerReady,
            ProxyLogSummarySourceMapper.FromStatus(logPersistence),
            ProxyShutdownSummarySourceMapper.FromRuntimeSummary(runtime));
    }
}

public static class ProxyStatusReadinessInputMapper
{
    public static ProxyStatusReadinessInput FromSources(
        ProxyStatusReadinessSourceSet sources,
        ProxyCacheStatusResponse? cacheStatus,
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
