using MDRAVA.BLL.ControlPlane.ConfigLint;
using MDRAVA.BLL.ControlPlane.RouteDiagnostics;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyMetricsSnapshot(
    long AcceptedConnections,
    long ActiveConnections,
    long TotalRequests,
    long UpstreamSuccesses,
    long UpstreamFailures,
    long BytesRead,
    long BytesWritten,
    long ParseErrors,
    ProxyRejectionMetricsSnapshot Rejections,
    long UpstreamMalformedResponses,
    long ClientBodyRelayFailures,
    long UpstreamBodyRelayFailures,
    long ClientRequestHeadTimeouts,
    long ClientRequestBodyTimeouts,
    long UpstreamConnectFailures,
    long UpstreamConnectTimeouts,
    long UpstreamResponseHeadTimeouts,
    long UpstreamResponseBodyTimeouts,
    long UpstreamPrematureDisconnects,
    long ClientPrematureDisconnects,
    long ProxyGenerated502Responses,
    long ProxyGenerated504Responses,
    long DownstreamWriteTimeouts,
    ProxyTlsMetricsSnapshot Tls,
    long ClientConnectionsClosedByIdleTimeout,
    long ClientConnectionsClosedByMaxRequests,
    ProxyUpstreamPoolMetricsSnapshot UpstreamPool,
    ProxyUpgradeMetricsSnapshot Upgrades,
    ProxyTunnelMetricsSnapshot Tunnels,
    long UpstreamSelections,
    ProxyHealthMetricsSnapshot Health,
    long UpstreamRequestFailures,
    long RequestIdsGenerated,
    long AccessLogsEmitted,
    long RecentDiagnosticsOverwritten,
    IReadOnlyDictionary<string, long> RequestFailuresByKind,
    IReadOnlyList<ProxyRequestSeriesSnapshot> RequestsByRoute,
    long ConfigReloadSuccesses,
    long ConfigReloadFailures,
    long AdminAuthSuccesses,
    long AdminAuthFailures,
    long AcmeRenewalAttempts,
    long AcmeRenewalSuccesses,
    long AcmeRenewalFailures,
    ProxyResilienceMetricsSnapshot Resilience,
    IReadOnlyList<ProxyUpstreamSelectionSnapshot> UpstreamSelectionsByUpstream,
    ProxyListenerMetricsSnapshot Listeners,
    ProxyHttp2MetricsSnapshot Http2,
    ProxyUpstreamHttp2MetricsSnapshot UpstreamHttp2,
    ProxyUpstreamHttp3MetricsSnapshot UpstreamHttp3,
    ProxyHttp3MetricsSnapshot Http3,
    long ConfigLintRuns,
    IReadOnlyList<ProxyConfigLintFindingMetricSnapshot> ConfigLintFindings,
    long RouteMatchDryRuns,
    IReadOnlyList<ProxyRouteDryRunFailureSnapshot> RouteMatchDryRunFailures)
{
    public ProxyHttp3MetricsSnapshot Http3 { get; } =
        Http3 ?? throw new ArgumentNullException(nameof(Http3));

    public ProxyUpstreamHttp3MetricsSnapshot UpstreamHttp3 { get; } =
        UpstreamHttp3 ?? throw new ArgumentNullException(nameof(UpstreamHttp3));

    public ProxyUpstreamHttp2MetricsSnapshot UpstreamHttp2 { get; } =
        UpstreamHttp2 ?? throw new ArgumentNullException(nameof(UpstreamHttp2));

    public ProxyHttp2MetricsSnapshot Http2 { get; } =
        Http2 ?? throw new ArgumentNullException(nameof(Http2));

    public ProxyListenerMetricsSnapshot Listeners { get; } =
        Listeners ?? throw new ArgumentNullException(nameof(Listeners));

    public ProxyUpstreamPoolMetricsSnapshot UpstreamPool { get; } =
        UpstreamPool ?? throw new ArgumentNullException(nameof(UpstreamPool));

    public ProxyUpgradeMetricsSnapshot Upgrades { get; } =
        Upgrades ?? throw new ArgumentNullException(nameof(Upgrades));

    public ProxyHealthMetricsSnapshot Health { get; } =
        Health ?? throw new ArgumentNullException(nameof(Health));

    public ProxyRejectionMetricsSnapshot Rejections { get; } =
        Rejections ?? throw new ArgumentNullException(nameof(Rejections));

    public ProxyResilienceMetricsSnapshot Resilience { get; } =
        Resilience ?? throw new ArgumentNullException(nameof(Resilience));

    public ProxyTunnelMetricsSnapshot Tunnels { get; } =
        Tunnels ?? throw new ArgumentNullException(nameof(Tunnels));

    public ProxyTlsMetricsSnapshot Tls { get; } =
        Tls ?? throw new ArgumentNullException(nameof(Tls));

    public IReadOnlyDictionary<string, long> RequestFailuresByKind { get; } =
        MetricsList.CopyDictionary(RequestFailuresByKind, StringComparer.Ordinal);

    public IReadOnlyList<ProxyRequestSeriesSnapshot> RequestsByRoute { get; } =
        MetricsList.Copy(RequestsByRoute);

    public IReadOnlyList<ProxyUpstreamSelectionSnapshot> UpstreamSelectionsByUpstream { get; } =
        MetricsList.Copy(UpstreamSelectionsByUpstream);

    public IReadOnlyList<ProxyConfigLintFindingMetricSnapshot> ConfigLintFindings { get; } =
        MetricsList.Copy(ConfigLintFindings);

    public IReadOnlyList<ProxyRouteDryRunFailureSnapshot> RouteMatchDryRunFailures { get; } =
        MetricsList.Copy(RouteMatchDryRunFailures);
}
