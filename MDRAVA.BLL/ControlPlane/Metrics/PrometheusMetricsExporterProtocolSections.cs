using System.Text;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class PrometheusMetricsExporter
{
    private static void AppendClientProtocolMetrics(
        StringBuilder builder,
        ProxyMetricsExportInput input,
        ProxyMetricsSnapshot proxy)
    {
        var http2 = proxy.Http2;
        AppendCounter(builder, "mdrava_http2_connections_accepted_total", "Accepted HTTP/2 downstream client connections.", http2.AcceptedConnections);
        AppendCounter(builder, "mdrava_http2_requests_total", "HTTP/2 requests received by the dataplane.", http2.Requests);
        AppendGauge(builder, "mdrava_http2_streams_active", "Currently active HTTP/2 streams.", http2.ActiveStreams);
        if (http2.ProtocolErrors.Count > 0)
        {
            AppendHelpAndType(builder, "mdrava_http2_protocol_errors_total", "HTTP/2 protocol errors by bounded reason.", "counter");
            foreach (var error in http2.ProtocolErrors.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                AppendSample(builder, "mdrava_http2_protocol_errors_total", error.Value, new Label("reason", error.Key));
            }
        }

        var http3 = proxy.Http3;
        AppendCounter(builder, "mdrava_http3_connections_accepted_total", "Accepted HTTP/3 downstream client connections.", http3.AcceptedConnections);
        AppendGauge(builder, "mdrava_http3_connections_active", "Currently active HTTP/3 client connections.", http3.ActiveConnections);
        AppendCounter(builder, "mdrava_http3_requests_total", "HTTP/3 requests received by the dataplane.", http3.Requests);
        if (http3.RequestsByOutcome.Count > 0)
        {
            AppendHelpAndType(builder, "mdrava_http3_requests_by_outcome_total", "HTTP/3 requests by bounded method, outcome, and status class.", "counter");
            foreach (var request in http3.RequestsByOutcome)
            {
                AppendSample(
                    builder,
                    "mdrava_http3_requests_by_outcome_total",
                    request.Count,
                    new Label("method", request.Method),
                    new Label("outcome", request.Outcome),
                    new Label("status_class", request.StatusClass));
            }
        }

        AppendCounter(builder, "mdrava_http3_proxied_requests_total", "HTTP/3 requests sent through proxy routes.", http3.ProxiedRequests);
        AppendCounter(builder, "mdrava_http3_generated_responses_total", "HTTP/3 generated route or control responses.", http3.GeneratedResponses);
        AppendGauge(builder, "mdrava_http3_streams_active", "Currently active HTTP/3 request streams.", http3.ActiveStreams);
        AppendCounter(builder, "mdrava_http3_stream_resets_total", "HTTP/3 request stream resets or cancellations.", http3.StreamResets);
        AppendCounter(builder, "mdrava_http3_streamed_responses_total", "HTTP/3 proxied responses streamed over DATA frames.", http3.StreamedResponses);
        AppendGauge(builder, "mdrava_http3_response_streams_active", "Currently active HTTP/3 response body streams.", http3.ActiveResponseStreams);
        AppendCounter(builder, "mdrava_http3_response_bytes_sent_total", "HTTP/3 response body bytes sent in DATA frames.", http3.ResponseBytesSent);
        AppendCounter(builder, "mdrava_http3_request_body_bytes_received_total", "HTTP/3 request body bytes accepted from DATA frames.", http3.RequestBodyBytesReceived);
        AppendCounter(builder, "mdrava_http3_response_stream_resets_total", "HTTP/3 response stream cancellations or write failures.", http3.ResponseStreamResets);
        AppendCounter(builder, "mdrava_http3_alt_svc_emitted_total", "HTTP/3 Alt-Svc headers emitted on proxy responses.", http3.AltSvcEmitted);
        AppendCounter(builder, "mdrava_http3_alt_svc_suppressed_total", "HTTP/3 Alt-Svc opportunities suppressed because HTTP/3 was disabled or not ready.", http3.AltSvcSuppressed);
        AppendGauge(builder, "mdrava_http3_default_enabled_listeners", "Configured default-enabled HTTP/3 proxy listeners.", input.DefaultEnabledHttp3ListenerCount);
        AppendGauge(builder, "mdrava_http3_qpack_dynamic_table_capacity", "Configured HTTP/3 QPACK dynamic table capacity. MDRAVA advertises zero for bounded static-table operation.", 0);
        AppendGauge(builder, "mdrava_http3_qpack_blocked_streams", "Configured HTTP/3 QPACK blocked streams. MDRAVA advertises zero to avoid blocked-stream accumulation.", 0);
        AppendGauge(builder, "mdrava_http3_request_body_streaming_enabled", "Whether HTTP/3 request body streaming is enabled for eligible proxy listeners.", input.Http3RequestBodyStreamingEnabled ? 1 : 0);
        AppendGauge(builder, "mdrava_quic_listeners_active", "Currently active HTTP/3 QUIC listeners.", http3.ActiveQuicListeners);
        AppendLabeledCounter(builder, "mdrava_quic_listener_starts_total", "HTTP/3 QUIC listener starts by result.", http3.QuicListenerStartSuccesses, new Label("result", "success"));
        AppendLabeledCounter(builder, "mdrava_quic_listener_starts_total", null, http3.QuicListenerStartFailures, new Label("result", "failure"));
        if (http3.RejectedRequests.Count > 0)
        {
            AppendHelpAndType(builder, "mdrava_http3_rejected_requests_total", "HTTP/3 request rejections by bounded reason.", "counter");
            foreach (var rejection in http3.RejectedRequests.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                AppendSample(builder, "mdrava_http3_rejected_requests_total", rejection.Value, new Label("reason", rejection.Key));
            }
        }

        if (http3.ProtocolErrors.Count > 0)
        {
            AppendHelpAndType(builder, "mdrava_http3_protocol_errors_total", "HTTP/3 protocol errors by bounded reason.", "counter");
            foreach (var error in http3.ProtocolErrors.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                AppendSample(builder, "mdrava_http3_protocol_errors_total", error.Value, new Label("reason", error.Key));
            }
        }
    }
}
