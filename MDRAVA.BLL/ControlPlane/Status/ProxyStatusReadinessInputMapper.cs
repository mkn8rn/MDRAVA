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

public static class ProxyStatusReadinessSourceMapper
{
    public static ProxyStatusReadinessSourceSet FromSources(
        ProxyConfigurationSnapshot? configuration,
        ProxyStatusRuntimeSummary runtime,
        ProxyMetricsSnapshot metrics,
        IReadOnlyList<ProxyUpstreamStatusResponse> upstreams,
        RuntimeHttp3SupportProjection http3,
        ProxyLogPersistenceStatus logPersistence)
    {
        return new ProxyStatusReadinessSourceSet(
            configuration is not null,
            configuration?.Version,
            configuration?.LoadedAtUtc,
            runtime.LastListenerReload?.Succeeded,
            runtime.LastListenerReload is { Succeeded: false },
            configuration is null
                ? []
                : ProxyConfiguredListenerSummarySourceMapper.FromListeners(configuration.Listeners),
            ProxyRuntimeListenerSummarySourceMapper.FromRuntimeSummary(runtime),
            configuration is null
                ? []
                : ProxyRouteSummarySourceMapper.FromRoutes(configuration.Routes),
            configuration is null
                ? null
                : ProxyCertificateSummarySourceMapper.FromConfiguration(
                    configuration.Listeners,
                    configuration.Certificates),
            configuration is null
                ? null
                : ProxyAcmeSummaryConfigurationSourceMapper.FromConfiguration(configuration.Acme),
            ProxyUpstreamSummarySourceMapper.FromStatusResponses(upstreams),
            ProxyLimitSummarySourceMapper.FromConfiguration(configuration),
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
