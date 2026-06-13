using MDRAVA.BLL.ControlPlane.Status;
using System.Text;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class PrometheusMetricsExporter
{
    private static void AppendUpstreamAndResilienceMetrics(
        StringBuilder builder,
        ProxyMetricsExportInput input,
        ProxyMetricsSnapshot proxy,
        IReadOnlyList<ProxyUpstreamStatus> health)
    {
        var upstreamHttp3 = proxy.UpstreamHttp3;
        var upstreamHttp2 = proxy.UpstreamHttp2;
        var upstreamPool = proxy.UpstreamPool;
        var healthMetrics = proxy.Health;
        var resilience = proxy.Resilience;
        var upstreamFailureReasons = proxy.UpstreamFailureReasons;
        AppendCounter(builder, "mdrava_upstream_request_attempts_total", "Selected upstream request attempts.", proxy.UpstreamSelections);
        AppendCounter(builder, "mdrava_upstream_http2_requests_total", "Upstream HTTP/2 request attempts.", upstreamHttp2.Requests);
        AppendCounter(builder, "mdrava_upstream_http3_requests_total", "Upstream HTTP/3 request attempts.", upstreamHttp3.Requests);
        AppendCounter(builder, "mdrava_upstream_http3_connection_attempts_total", "Upstream HTTP/3 QUIC connection attempts.", upstreamHttp3.ConnectionAttempts);
        AppendCounter(builder, "mdrava_upstream_http3_connection_successes_total", "Successful upstream HTTP/3 QUIC connections.", upstreamHttp3.ConnectionSuccesses);
        AppendCounter(builder, "mdrava_upstream_http3_connection_failures_total", "Failed upstream HTTP/3 QUIC connections.", upstreamHttp3.ConnectionFailures);
        AppendCounter(builder, "mdrava_upstream_http3_pool_connections_opened_total", "Upstream HTTP/3 pool connections opened.", upstreamHttp3.PoolConnectionsOpened);
        AppendCounter(builder, "mdrava_upstream_http3_pool_connections_reused_total", "Upstream HTTP/3 pool connection reuses.", upstreamHttp3.PoolConnectionsReused);
        AppendCounter(builder, "mdrava_upstream_http3_pool_connections_closed_total", "Upstream HTTP/3 pool connections closed.", upstreamHttp3.PoolConnectionsClosed);
        AppendCounter(builder, "mdrava_upstream_http3_stream_limit_rejections_total", "Upstream HTTP/3 stream limit rejections.", upstreamHttp3.StreamLimitRejections);
        AppendGauge(builder, "mdrava_upstream_http3_multiplexing_enabled", "Whether upstream HTTP/3 multiplexing is enabled.", upstreamHttp3.Requests > 0 || input.UpstreamHttp3MultiplexingConfigured ? 1 : 0);
        AppendGauge(builder, "mdrava_upstream_http3_connections_active", "Active upstream HTTP/3 QUIC connections.", upstreamHttp3.ActiveConnections);
        AppendGauge(builder, "mdrava_upstream_http3_streams_active", "Active upstream HTTP/3 streams.", upstreamHttp3.ActiveStreams);
        AppendUpstreamSelectionCounters(builder, input.IncludePerUpstreamLabels, proxy.UpstreamSelectionsByUpstream);
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", "Upstream failures by bounded reason.", upstreamFailureReasons.ConnectFailures, new Label("reason", "connect_failure"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, upstreamFailureReasons.ConnectTimeouts, new Label("reason", "connect_timeout"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, upstreamFailureReasons.ResponseHeadTimeouts, new Label("reason", "response_head_timeout"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, upstreamFailureReasons.ResponseBodyTimeouts, new Label("reason", "response_body_timeout"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, upstreamFailureReasons.MalformedResponses, new Label("reason", "malformed_response"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, upstreamFailureReasons.PrematureDisconnects, new Label("reason", "premature_disconnect"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, healthMetrics.NoHealthyUpstreamFailures, new Label("reason", "no_healthy_upstream"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, resilience.NoAvailableUpstreamFailures, new Label("reason", "no_available_upstream"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, upstreamFailureReasons.RequestFailures, new Label("reason", "request_failure"));
        AppendLabeledCounter(builder, "mdrava_upstream_http2_failures_total", "Upstream HTTP/2 failures by bounded reason.", upstreamHttp2.AlpnFailures, new Label("reason", "alpn_failure"));
        AppendLabeledCounter(builder, "mdrava_upstream_http2_failures_total", null, upstreamHttp2.ProtocolErrors, new Label("reason", "protocol_error"));
        if (upstreamHttp3.ProtocolErrors.Count > 0)
        {
            AppendHelpAndType(builder, "mdrava_upstream_http3_protocol_errors_total", "Upstream HTTP/3 protocol errors by bounded reason.", "counter");
            foreach (var error in upstreamHttp3.ProtocolErrors.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                AppendSample(builder, "mdrava_upstream_http3_protocol_errors_total", error.Value, new Label("reason", error.Key));
            }
        }

        AppendCounter(builder, "mdrava_retry_attempts_total", "Retry attempts after an initial failed upstream attempt.", resilience.RetryAttempts);
        AppendCounter(builder, "mdrava_retry_exhausted_total", "Requests that exhausted their configured retry attempts.", resilience.RetryExhausted);
        if (resilience.RetrySkipped.Count > 0)
        {
            AppendHelpAndType(builder, "mdrava_retry_skipped_total", "Retries skipped by bounded reason.", "counter");
        }

        foreach (var skipped in resilience.RetrySkipped)
        {
            AppendSample(builder, "mdrava_retry_skipped_total", skipped.Count, new Label("reason", skipped.Reason));
        }

        AppendLabeledCounter(builder, "mdrava_circuit_transitions_total", "Circuit breaker transitions by state.", resilience.CircuitOpened, new Label("state", "open"));
        AppendLabeledCounter(builder, "mdrava_circuit_transitions_total", null, resilience.CircuitHalfOpened, new Label("state", "half_open"));
        AppendLabeledCounter(builder, "mdrava_circuit_transitions_total", null, resilience.CircuitClosed, new Label("state", "closed"));
        AppendCounter(builder, "mdrava_circuit_rejections_total", "Requests rejected by open or saturated half-open circuits.", resilience.CircuitRejections);

        AppendGauge(builder, "mdrava_upstream_connections_active", "Active borrowed upstream connections.", upstreamPool.ActiveConnections);
        AppendGauge(builder, "mdrava_upstream_connections_idle", "Idle reusable upstream connections.", upstreamPool.IdleConnections);
        AppendCounter(builder, "mdrava_upstream_connections_opened_total", "Opened upstream connections.", upstreamPool.ConnectionsOpened);
        AppendCounter(builder, "mdrava_upstream_connections_reused_total", "Reused upstream connections.", upstreamPool.ConnectionsReused);
        AppendCounter(builder, "mdrava_upstream_connections_discarded_total", "Discarded upstream connections.", upstreamPool.ConnectionsDiscarded);

        AppendLabeledCounter(builder, "mdrava_health_checks_total", "Health checks by result.", healthMetrics.ChecksAttempted, new Label("result", "attempted"));
        AppendLabeledCounter(builder, "mdrava_health_checks_total", null, healthMetrics.ChecksSucceeded, new Label("result", "success"));
        AppendLabeledCounter(builder, "mdrava_health_checks_total", null, healthMetrics.ChecksFailed, new Label("result", "failure"));
        AppendCounter(builder, "mdrava_upstream_health_transitions_total", "Upstream health state transitions.", healthMetrics.UpstreamTransitions);
        AppendUpstreamHealth(builder, input.IncludePerUpstreamLabels, health);
    }
}
