using MDRAVA.BLL.ControlPlane.ConfigLint;
using MDRAVA.BLL.ControlPlane.RouteDiagnostics;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed record ProxyMetricsSnapshot(
    ProxyClientConnectionMetricsSnapshot ClientConnections,
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
    ProxyUpstreamPoolMetricsSnapshot UpstreamPool,
    ProxyUpgradeMetricsSnapshot Upgrades,
    ProxyTunnelMetricsSnapshot Tunnels,
    long UpstreamSelections,
    ProxyHealthMetricsSnapshot Health,
    long UpstreamRequestFailures,
    ProxyDiagnosticsMetricsSnapshot Diagnostics,
    IReadOnlyDictionary<string, long> RequestFailuresByKind,
    IReadOnlyList<ProxyRequestSeriesSnapshot> RequestsByRoute,
    ProxyConfigReloadMetricsSnapshot ConfigReloads,
    ProxyAdminAuthMetricsSnapshot AdminAuth,
    ProxyAcmeRenewalMetricsSnapshot AcmeRenewals,
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
    public ProxyClientConnectionMetricsSnapshot ClientConnections { get; } =
        ClientConnections ?? throw new ArgumentNullException(nameof(ClientConnections));

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

    public ProxyDiagnosticsMetricsSnapshot Diagnostics { get; } =
        Diagnostics ?? throw new ArgumentNullException(nameof(Diagnostics));

    public ProxyConfigReloadMetricsSnapshot ConfigReloads { get; } =
        ConfigReloads ?? throw new ArgumentNullException(nameof(ConfigReloads));

    public ProxyAdminAuthMetricsSnapshot AdminAuth { get; } =
        AdminAuth ?? throw new ArgumentNullException(nameof(AdminAuth));

    public ProxyAcmeRenewalMetricsSnapshot AcmeRenewals { get; } =
        AcmeRenewals ?? throw new ArgumentNullException(nameof(AcmeRenewals));

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
