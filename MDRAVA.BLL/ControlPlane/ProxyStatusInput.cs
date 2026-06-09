using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane;

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
    ConfigLintStatus ConfigLint);

public interface IProxyStatusInputReader
{
    ProxyStatusInput Read();
}
