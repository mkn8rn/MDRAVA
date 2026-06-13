using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;

namespace MDRAVA.BLL.ControlPlane.Status;

public sealed record ProxyStatusReadinessInput(
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
    ProxyShutdownSummarySource Shutdown,
    ProxyCacheStatus? CacheStatus,
    IReadOnlyList<AcmeCertificateLifecycleStatus> AcmeStatuses,
    ProxyRuntimePreflightStatus RuntimePreflight,
    DateTimeOffset ObservedAtUtc);
