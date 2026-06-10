using MDRAVA.BLL.ControlPlane.Resilience;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane;
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

public sealed record ProxyRouteSummarySource(
    string SiteName,
    bool IsProxyRoute,
    bool CacheEnabled,
    bool HasHttp3Upstream);

public sealed record ProxyCertificateSummarySource(
    IReadOnlyList<string> ReferencedCertificateIds,
    IReadOnlyList<ProxyCertificateValiditySource> LoadedCertificates);

public sealed record ProxyCertificateValiditySource(
    string Id,
    DateTime NotBefore,
    DateTime NotAfter);

public sealed record ProxyAcmeSummaryConfigurationSource(
    bool Enabled,
    int ConfiguredCertificates);

public sealed record ProxyUpstreamSummarySource(
    UpstreamHealthState HealthState,
    bool HealthCheckEnabled,
    bool CircuitBreakerEnabled,
    CircuitBreakerRuntimeState CircuitBreakerState);

public sealed record ProxyLimitConfigurationSummarySource(
    int MaxActiveClientConnections,
    int MaxConcurrentTlsHandshakes,
    int RequestsPerMinutePerIp);

public sealed record ProxyLimitRuntimeSummarySource(
    long ActiveConnections,
    long ActiveTlsHandshakes,
    long ActiveHttp2Streams,
    long ActiveHttp3Streams,
    long ActiveUpstreamHttp3Streams);

public sealed record ProxyLogSummarySource(
    bool AccessLogPersistenceEnabled,
    bool AdminAuditPersistenceEnabled,
    string State,
    string Reason);

public sealed record ProxyShutdownSummarySource(
    bool IsRunning,
    bool IsShuttingDown,
    DateTimeOffset? ShutdownStartedAtUtc,
    DateTimeOffset? ShutdownDeadlineUtc);
