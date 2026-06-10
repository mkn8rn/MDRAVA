using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.Caching;
using MDRAVA.BLL.ControlPlane.ConfigLint;
using MDRAVA.BLL.ControlPlane.Http3;
using MDRAVA.BLL.ControlPlane.Listeners;
using MDRAVA.BLL.ControlPlane.Metrics;

namespace MDRAVA.BLL.ControlPlane.Status;

public sealed record ProxyStatusInput(
    ProxyRuntimeSnapshot Runtime,
    ProxyConfigurationSnapshot? Configuration,
    ProxyMetricsSnapshot Metrics,
    IReadOnlyList<ProxyUpstreamStatusResponse> Upstreams,
    RuntimeHttp3SupportProjection Http3,
    ProxyLogPersistenceStatus LogPersistence,
    ProxyCacheStatusResponse? CacheStatus,
    IReadOnlyList<AcmeCertificateLifecycleStatus> AcmeStatuses,
    ProxyRuntimePreflightStatus RuntimePreflight,
    DateTimeOffset ObservedAtUtc,
    ConfigLintStatus ConfigLint);

public interface IProxyStatusInputReader
{
    ProxyStatusInput Read();
}
