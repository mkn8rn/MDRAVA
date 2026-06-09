using System.Globalization;
using System.Text;
using MDRAVA.API.Proxy.Acme;
using MDRAVA.API.Proxy.Caching;
using MDRAVA.API.Proxy.Health;

namespace MDRAVA.API.Proxy.Metrics;

public sealed class PrometheusMetricsExporter
{
    public const string ContentType = "text/plain; version=0.0.4; charset=utf-8";

    private readonly ProxyMetrics _metrics;
    private readonly ResponseCacheStore _cacheStore;
    private readonly UpstreamHealthStore _healthStore;
    private readonly AcmeCertificateStatusStore _acmeStatusStore;

    public PrometheusMetricsExporter(
        ProxyMetrics metrics,
        ResponseCacheStore cacheStore,
        UpstreamHealthStore healthStore,
        AcmeCertificateStatusStore acmeStatusStore)
    {
        _metrics = metrics;
        _cacheStore = cacheStore;
        _healthStore = healthStore;
        _acmeStatusStore = acmeStatusStore;
    }

    public string Export(ProxyConfigurationSnapshot snapshot)
    {
        var proxy = _metrics.Snapshot();
        var cache = ProxyCacheStatusReader.Project(
            ProxyCacheStatusRouteSourceMapper.ToRouteSources(snapshot),
            _cacheStore.ReadStatusSnapshot());
        var health = _healthStore.Snapshot(snapshot);
        var acme = _acmeStatusStore.Snapshot();
        var builder = new StringBuilder();

        AppendCounter(builder, "mdrava_client_connections_accepted_total", "Accepted downstream client connections.", proxy.AcceptedConnections);
        AppendGauge(builder, "mdrava_client_connections_active", "Currently active downstream client connections.", proxy.ActiveConnections);
        AppendLabeledCounter(builder, "mdrava_client_connections_rejected_total", "Rejected downstream client connections by bounded reason.", proxy.ConnectionAdmissionRejections, new Label("reason", "admission_limit"));

        AppendCounter(builder, "mdrava_requests_total", "HTTP requests received by the dataplane.", proxy.TotalRequests);
        AppendCounter(builder, "mdrava_http2_connections_accepted_total", "Accepted HTTP/2 downstream client connections.", proxy.Http2AcceptedConnections);
        AppendCounter(builder, "mdrava_http2_requests_total", "HTTP/2 requests received by the dataplane.", proxy.Http2Requests);
        AppendGauge(builder, "mdrava_http2_streams_active", "Currently active HTTP/2 streams.", proxy.ActiveHttp2Streams);
        if (proxy.Http2ProtocolErrors.Count > 0)
        {
            AppendHelpAndType(builder, "mdrava_http2_protocol_errors_total", "HTTP/2 protocol errors by bounded reason.", "counter");
            foreach (var error in proxy.Http2ProtocolErrors.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                AppendSample(builder, "mdrava_http2_protocol_errors_total", error.Value, new Label("reason", error.Key));
            }
        }

        AppendCounter(builder, "mdrava_http3_connections_accepted_total", "Accepted HTTP/3 downstream client connections.", proxy.Http3AcceptedConnections);
        AppendGauge(builder, "mdrava_http3_connections_active", "Currently active HTTP/3 client connections.", proxy.ActiveHttp3Connections);
        AppendCounter(builder, "mdrava_http3_requests_total", "HTTP/3 requests received by the dataplane.", proxy.Http3Requests);
        if (proxy.Http3RequestsByOutcome.Count > 0)
        {
            AppendHelpAndType(builder, "mdrava_http3_requests_by_outcome_total", "HTTP/3 requests by bounded method, outcome, and status class.", "counter");
            foreach (var request in proxy.Http3RequestsByOutcome)
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

        AppendCounter(builder, "mdrava_http3_proxied_requests_total", "HTTP/3 requests sent through proxy routes.", proxy.Http3ProxiedRequests);
        AppendCounter(builder, "mdrava_http3_generated_responses_total", "HTTP/3 generated route or control responses.", proxy.Http3GeneratedResponses);
        AppendGauge(builder, "mdrava_http3_streams_active", "Currently active HTTP/3 request streams.", proxy.ActiveHttp3Streams);
        AppendCounter(builder, "mdrava_http3_stream_resets_total", "HTTP/3 request stream resets or cancellations.", proxy.Http3StreamResets);
        AppendCounter(builder, "mdrava_http3_streamed_responses_total", "HTTP/3 proxied responses streamed over DATA frames.", proxy.Http3StreamedResponses);
        AppendGauge(builder, "mdrava_http3_response_streams_active", "Currently active HTTP/3 response body streams.", proxy.ActiveHttp3ResponseStreams);
        AppendCounter(builder, "mdrava_http3_response_bytes_sent_total", "HTTP/3 response body bytes sent in DATA frames.", proxy.Http3ResponseBytesSent);
        AppendCounter(builder, "mdrava_http3_request_body_bytes_received_total", "HTTP/3 request body bytes accepted from DATA frames.", proxy.Http3RequestBodyBytesReceived);
        AppendCounter(builder, "mdrava_http3_response_stream_resets_total", "HTTP/3 response stream cancellations or write failures.", proxy.Http3ResponseStreamResets);
        AppendCounter(builder, "mdrava_http3_alt_svc_emitted_total", "HTTP/3 Alt-Svc headers emitted on proxy responses.", proxy.Http3AltSvcEmitted);
        AppendCounter(builder, "mdrava_http3_alt_svc_suppressed_total", "HTTP/3 Alt-Svc opportunities suppressed because HTTP/3 was disabled or not ready.", proxy.Http3AltSvcSuppressed);
        AppendGauge(builder, "mdrava_http3_default_enabled_listeners", "Configured default-enabled HTTP/3 proxy listeners.", snapshot.Listeners.Count(static listener => listener.Http3.EnabledForTraffic && string.Equals(listener.Http3.EnablementLevel, "default", StringComparison.OrdinalIgnoreCase)));
        AppendGauge(builder, "mdrava_http3_qpack_dynamic_table_capacity", "Configured HTTP/3 QPACK dynamic table capacity. MDRAVA advertises zero for bounded static-table operation.", 0);
        AppendGauge(builder, "mdrava_http3_qpack_blocked_streams", "Configured HTTP/3 QPACK blocked streams. MDRAVA advertises zero to avoid blocked-stream accumulation.", 0);
        AppendGauge(builder, "mdrava_http3_request_body_streaming_enabled", "Whether HTTP/3 request body streaming is enabled for eligible proxy listeners.", snapshot.Listeners.Any(static listener => listener.Http3.EnabledForTraffic) ? 1 : 0);
        AppendGauge(builder, "mdrava_quic_listeners_active", "Currently active HTTP/3 QUIC listeners.", proxy.ActiveQuicListeners);
        AppendLabeledCounter(builder, "mdrava_quic_listener_starts_total", "HTTP/3 QUIC listener starts by result.", proxy.QuicListenerStartSuccesses, new Label("result", "success"));
        AppendLabeledCounter(builder, "mdrava_quic_listener_starts_total", null, proxy.QuicListenerStartFailures, new Label("result", "failure"));
        if (proxy.Http3RejectedRequests.Count > 0)
        {
            AppendHelpAndType(builder, "mdrava_http3_rejected_requests_total", "HTTP/3 request rejections by bounded reason.", "counter");
            foreach (var rejection in proxy.Http3RejectedRequests.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                AppendSample(builder, "mdrava_http3_rejected_requests_total", rejection.Value, new Label("reason", rejection.Key));
            }
        }

        if (proxy.Http3ProtocolErrors.Count > 0)
        {
            AppendHelpAndType(builder, "mdrava_http3_protocol_errors_total", "HTTP/3 protocol errors by bounded reason.", "counter");
            foreach (var error in proxy.Http3ProtocolErrors.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                AppendSample(builder, "mdrava_http3_protocol_errors_total", error.Value, new Label("reason", error.Key));
            }
        }

        AppendRouteRequestCounters(builder, snapshot.Metrics, proxy.RequestsByRoute);
        AppendRequestRejectionCounters(builder, proxy);

        AppendCounter(builder, "mdrava_upstream_request_attempts_total", "Selected upstream request attempts.", proxy.UpstreamSelections);
        AppendCounter(builder, "mdrava_upstream_http2_requests_total", "Upstream HTTP/2 request attempts.", proxy.UpstreamHttp2Requests);
        AppendCounter(builder, "mdrava_upstream_http3_requests_total", "Upstream HTTP/3 request attempts.", proxy.UpstreamHttp3Requests);
        AppendCounter(builder, "mdrava_upstream_http3_connection_attempts_total", "Upstream HTTP/3 QUIC connection attempts.", proxy.UpstreamHttp3ConnectionAttempts);
        AppendCounter(builder, "mdrava_upstream_http3_connection_successes_total", "Successful upstream HTTP/3 QUIC connections.", proxy.UpstreamHttp3ConnectionSuccesses);
        AppendCounter(builder, "mdrava_upstream_http3_connection_failures_total", "Failed upstream HTTP/3 QUIC connections.", proxy.UpstreamHttp3ConnectionFailures);
        AppendCounter(builder, "mdrava_upstream_http3_pool_connections_opened_total", "Upstream HTTP/3 pool connections opened.", proxy.UpstreamHttp3PoolConnectionsOpened);
        AppendCounter(builder, "mdrava_upstream_http3_pool_connections_reused_total", "Upstream HTTP/3 pool connection reuses.", proxy.UpstreamHttp3PoolConnectionsReused);
        AppendCounter(builder, "mdrava_upstream_http3_pool_connections_closed_total", "Upstream HTTP/3 pool connections closed.", proxy.UpstreamHttp3PoolConnectionsClosed);
        AppendCounter(builder, "mdrava_upstream_http3_stream_limit_rejections_total", "Upstream HTTP/3 stream limit rejections.", proxy.UpstreamHttp3StreamLimitRejections);
        AppendGauge(builder, "mdrava_upstream_http3_multiplexing_enabled", "Whether upstream HTTP/3 multiplexing is enabled.", proxy.UpstreamHttp3Requests > 0 || snapshot.Routes.Any(static route => route.Upstreams.Any(static upstream => RuntimeUpstreamProtocol.IsHttp3(upstream.Protocol))) ? 1 : 0);
        AppendGauge(builder, "mdrava_upstream_http3_connections_active", "Active upstream HTTP/3 QUIC connections.", proxy.ActiveUpstreamHttp3Connections);
        AppendGauge(builder, "mdrava_upstream_http3_streams_active", "Active upstream HTTP/3 streams.", proxy.ActiveUpstreamHttp3Streams);
        AppendUpstreamSelectionCounters(builder, snapshot.Metrics, proxy.UpstreamSelectionsByUpstream);
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", "Upstream failures by bounded reason.", proxy.UpstreamConnectFailures, new Label("reason", "connect_failure"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, proxy.UpstreamConnectTimeouts, new Label("reason", "connect_timeout"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, proxy.UpstreamResponseHeadTimeouts, new Label("reason", "response_head_timeout"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, proxy.UpstreamResponseBodyTimeouts, new Label("reason", "response_body_timeout"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, proxy.UpstreamMalformedResponses, new Label("reason", "malformed_response"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, proxy.UpstreamPrematureDisconnects, new Label("reason", "premature_disconnect"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, proxy.NoHealthyUpstreamFailures, new Label("reason", "no_healthy_upstream"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, proxy.NoAvailableUpstreamFailures, new Label("reason", "no_available_upstream"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, proxy.UpstreamRequestFailures, new Label("reason", "request_failure"));
        AppendLabeledCounter(builder, "mdrava_upstream_http2_failures_total", "Upstream HTTP/2 failures by bounded reason.", proxy.UpstreamHttp2AlpnFailures, new Label("reason", "alpn_failure"));
        AppendLabeledCounter(builder, "mdrava_upstream_http2_failures_total", null, proxy.UpstreamHttp2ProtocolErrors, new Label("reason", "protocol_error"));
        if (proxy.UpstreamHttp3ProtocolErrors.Count > 0)
        {
            AppendHelpAndType(builder, "mdrava_upstream_http3_protocol_errors_total", "Upstream HTTP/3 protocol errors by bounded reason.", "counter");
            foreach (var error in proxy.UpstreamHttp3ProtocolErrors.OrderBy(static item => item.Key, StringComparer.Ordinal))
            {
                AppendSample(builder, "mdrava_upstream_http3_protocol_errors_total", error.Value, new Label("reason", error.Key));
            }
        }

        AppendCounter(builder, "mdrava_retry_attempts_total", "Retry attempts after an initial failed upstream attempt.", proxy.RetryAttempts);
        AppendCounter(builder, "mdrava_retry_exhausted_total", "Requests that exhausted their configured retry attempts.", proxy.RetryExhausted);
        if (proxy.RetrySkipped.Count > 0)
        {
            AppendHelpAndType(builder, "mdrava_retry_skipped_total", "Retries skipped by bounded reason.", "counter");
        }

        foreach (var skipped in proxy.RetrySkipped)
        {
            AppendSample(builder, "mdrava_retry_skipped_total", skipped.Count, new Label("reason", skipped.Reason));
        }

        AppendLabeledCounter(builder, "mdrava_circuit_transitions_total", "Circuit breaker transitions by state.", proxy.CircuitOpened, new Label("state", "open"));
        AppendLabeledCounter(builder, "mdrava_circuit_transitions_total", null, proxy.CircuitHalfOpened, new Label("state", "half_open"));
        AppendLabeledCounter(builder, "mdrava_circuit_transitions_total", null, proxy.CircuitClosed, new Label("state", "closed"));
        AppendCounter(builder, "mdrava_circuit_rejections_total", "Requests rejected by open or saturated half-open circuits.", proxy.CircuitRejections);

        AppendGauge(builder, "mdrava_upstream_connections_active", "Active borrowed upstream connections.", proxy.UpstreamPoolActiveConnections);
        AppendGauge(builder, "mdrava_upstream_connections_idle", "Idle reusable upstream connections.", proxy.UpstreamPoolIdleConnections);
        AppendCounter(builder, "mdrava_upstream_connections_opened_total", "Opened upstream connections.", proxy.UpstreamConnectionsOpened);
        AppendCounter(builder, "mdrava_upstream_connections_reused_total", "Reused upstream connections.", proxy.UpstreamConnectionsReused);
        AppendCounter(builder, "mdrava_upstream_connections_discarded_total", "Discarded upstream connections.", proxy.UpstreamConnectionsDiscarded);

        AppendLabeledCounter(builder, "mdrava_health_checks_total", "Health checks by result.", proxy.HealthChecksAttempted, new Label("result", "attempted"));
        AppendLabeledCounter(builder, "mdrava_health_checks_total", null, proxy.HealthChecksSucceeded, new Label("result", "success"));
        AppendLabeledCounter(builder, "mdrava_health_checks_total", null, proxy.HealthChecksFailed, new Label("result", "failure"));
        AppendCounter(builder, "mdrava_upstream_health_transitions_total", "Upstream health state transitions.", proxy.UpstreamHealthTransitions);
        AppendUpstreamHealth(builder, snapshot.Metrics, health);

        AppendGauge(builder, "mdrava_cache_entries", "Current in-memory response cache entries.", cache.EntryCount);
        AppendGauge(builder, "mdrava_cache_bytes", "Approximate in-memory response cache bytes.", cache.ApproximateBytes);
        AppendCounter(builder, "mdrava_cache_hits_total", "Response cache hits.", cache.HitCount);
        AppendCounter(builder, "mdrava_cache_misses_total", "Response cache misses.", cache.MissCount);
        AppendCounter(builder, "mdrava_cache_stores_total", "Stored response cache entries.", cache.StoreCount);
        AppendCounter(builder, "mdrava_cache_evictions_total", "Evicted response cache entries.", cache.EvictionCount);
        if (cache.Rejections.Count > 0)
        {
            AppendHelpAndType(builder, "mdrava_cache_store_rejections_total", "Response cache store rejections by bounded reason.", "counter");
        }

        foreach (var rejection in cache.Rejections)
        {
            AppendSample(
                builder,
                "mdrava_cache_store_rejections_total",
                rejection.Count,
                new Label("reason", rejection.Reason));
        }

        AppendLabeledCounter(builder, "mdrava_config_reloads_total", "Configuration reloads by result.", proxy.ConfigReloadSuccesses, new Label("result", "success"));
        AppendLabeledCounter(builder, "mdrava_config_reloads_total", null, proxy.ConfigReloadFailures, new Label("result", "failure"));
        AppendCounter(builder, "mdrava_config_lint_runs_total", "Configuration lint runs.", proxy.ConfigLintRuns);
        if (proxy.ConfigLintFindings.Count > 0)
        {
            AppendHelpAndType(builder, "mdrava_config_lint_findings_total", "Configuration lint findings by bounded severity and code.", "counter");
            foreach (var finding in proxy.ConfigLintFindings)
            {
                AppendSample(
                    builder,
                    "mdrava_config_lint_findings_total",
                    finding.Count,
                    new Label("severity", finding.Severity),
                    new Label("code", finding.Code));
            }
        }

        AppendCounter(builder, "mdrava_route_match_dry_runs_total", "Route match dry-run requests.", proxy.RouteMatchDryRuns);
        if (proxy.RouteMatchDryRunFailures.Count > 0)
        {
            AppendHelpAndType(builder, "mdrava_route_match_dry_run_failures_total", "Route match dry-run non-match or rejection results by bounded reason.", "counter");
            foreach (var failure in proxy.RouteMatchDryRunFailures)
            {
                AppendSample(
                    builder,
                    "mdrava_route_match_dry_run_failures_total",
                    failure.Count,
                    new Label("reason", failure.Reason));
            }
        }

        AppendLabeledCounter(builder, "mdrava_listener_reloads_total", "Proxy listener reload attempts by result.", proxy.ListenerReloadSuccesses, new Label("result", "success"));
        AppendLabeledCounter(builder, "mdrava_listener_reloads_total", null, proxy.ListenerReloadFailures, new Label("result", "failure"));
        AppendCounter(builder, "mdrava_listener_reload_attempts_total", "Proxy listener reload attempts.", proxy.ListenerReloadAttempts);
        AppendLabeledCounter(builder, "mdrava_listener_reload_changes_total", "Proxy listener reload changes by bounded action.", proxy.ListenerReloadAdded, new Label("action", "added"));
        AppendLabeledCounter(builder, "mdrava_listener_reload_changes_total", null, proxy.ListenerReloadRemoved, new Label("action", "removed"));
        AppendLabeledCounter(builder, "mdrava_listener_reload_changes_total", null, proxy.ListenerReloadChanged, new Label("action", "changed"));
        AppendLabeledCounter(builder, "mdrava_listener_reload_changes_total", null, proxy.ListenerReloadUnchanged, new Label("action", "unchanged"));
        AppendCounter(builder, "mdrava_listener_start_failures_total", "Proxy listener start failures.", proxy.ListenerStartFailures);
        AppendCounter(builder, "mdrava_listener_drains_total", "Proxy listener drains after reload removal or replacement.", proxy.ListenerDrainCount);
        AppendGauge(builder, "mdrava_listeners_active", "Currently active proxy listeners.", proxy.ActiveListeners);
        AppendLabeledCounter(builder, "mdrava_admin_auth_total", "Admin authentication attempts by result.", proxy.AdminAuthSuccesses, new Label("result", "success"));
        AppendLabeledCounter(builder, "mdrava_admin_auth_total", null, proxy.AdminAuthFailures, new Label("result", "failure"));
        AppendLabeledCounter(builder, "mdrava_acme_renewals_total", "ACME renewal attempts by result.", proxy.AcmeRenewalAttempts, new Label("result", "attempt"));
        AppendLabeledCounter(builder, "mdrava_acme_renewals_total", null, proxy.AcmeRenewalSuccesses, new Label("result", "success"));
        AppendLabeledCounter(builder, "mdrava_acme_renewals_total", null, proxy.AcmeRenewalFailures, new Label("result", "failure"));
        AppendAcmeCertificateStatus(builder, acme);

        return builder.ToString();
    }

    private static void AppendRouteRequestCounters(
        StringBuilder builder,
        RuntimeMetricsOptions options,
        IReadOnlyList<ProxyRequestSeriesSnapshot> requests)
    {
        AppendHelpAndType(builder, "mdrava_route_requests_total", "Completed requests by bounded route/action/status labels.", "counter");
        IEnumerable<ProxyRequestSeriesSnapshot> series = options.IncludePerRouteLabels
            ? requests
            : requests
                .GroupBy(static request => new { request.Action, request.StatusClass })
                .Select(static group => new ProxyRequestSeriesSnapshot(
                    "all",
                    "all",
                    group.Key.Action,
                    group.Key.StatusClass,
                    group.Sum(static item => item.Count)));

        foreach (var request in series)
        {
            var labels = options.IncludePerRouteLabels
                ? new[]
                {
                    new Label("site", request.Site),
                    new Label("route", request.Route),
                    new Label("action", request.Action),
                    new Label("status_class", request.StatusClass)
                }
                : [new Label("action", request.Action), new Label("status_class", request.StatusClass)];
            AppendSample(builder, "mdrava_route_requests_total", request.Count, labels);
        }
    }

    private static void AppendRequestRejectionCounters(StringBuilder builder, ProxyMetricsSnapshot proxy)
    {
        AppendLabeledCounter(builder, "mdrava_request_rejections_total", "Request rejections by bounded reason.", proxy.RateLimitedRequests, new Label("reason", "rate_limited"));
        AppendLabeledCounter(builder, "mdrava_request_rejections_total", null, proxy.RateLimitedUpgrades, new Label("reason", "upgrade_rate_limited"));
        AppendLabeledCounter(builder, "mdrava_request_rejections_total", null, proxy.RequestBodySizeRejections, new Label("reason", "body_too_large"));
        AppendLabeledCounter(builder, "mdrava_request_rejections_total", null, proxy.ParserLimitRejections, new Label("reason", "parser_limit"));
        AppendLabeledCounter(builder, "mdrava_request_rejections_total", null, proxy.RejectedMalformedRequests, new Label("reason", "malformed"));
        AppendLabeledCounter(builder, "mdrava_request_rejections_total", null, proxy.RejectedUnsupportedRequestFraming, new Label("reason", "unsupported_framing"));
    }

    private static void AppendUpstreamSelectionCounters(
        StringBuilder builder,
        RuntimeMetricsOptions options,
        IReadOnlyList<ProxyUpstreamSelectionSnapshot> selections)
    {
        if (!options.IncludePerUpstreamLabels || selections.Count == 0)
        {
            return;
        }

        AppendHelpAndType(builder, "mdrava_upstream_selections_total", "Selected upstream count by bounded upstream labels.", "counter");
        foreach (var selection in selections)
        {
            AppendSample(
                builder,
                "mdrava_upstream_selections_total",
                selection.Count,
                new Label("route", selection.Route),
                new Label("upstream", selection.Upstream),
                new Label("scheme", selection.Scheme),
                new Label("protocol", selection.Protocol));
        }
    }

    private static void AppendUpstreamHealth(
        StringBuilder builder,
        RuntimeMetricsOptions options,
        IReadOnlyList<ProxyUpstreamStatusResponse> health)
    {
        if (!options.IncludePerUpstreamLabels)
        {
            return;
        }

        AppendHelpAndType(builder, "mdrava_upstream_health_up", "Current upstream health status, 1 for healthy and 0 otherwise.", "gauge");
        foreach (var upstream in health)
        {
            var value = upstream.HealthState == UpstreamHealthState.Healthy ? 1 : 0;
            AppendSample(
                builder,
                "mdrava_upstream_health_up",
                value,
                new Label("route", upstream.RouteName),
                new Label("upstream", upstream.UpstreamName),
                new Label("scheme", upstream.Scheme),
                new Label("protocol", upstream.Protocol),
                new Label("state", upstream.HealthState.ToString()));
        }
    }

    private static void AppendAcmeCertificateStatus(
        StringBuilder builder,
        IReadOnlyList<AcmeCertificateLifecycleStatus> statuses)
    {
        AppendHelpAndType(builder, "mdrava_acme_certificates", "Configured ACME certificate lifecycle statuses by bounded result.", "gauge");
        foreach (var group in statuses.GroupBy(static status => new { status.LastResult, status.Active }))
        {
            AppendSample(
                builder,
                "mdrava_acme_certificates",
                group.Count(),
                new Label("result", group.Key.LastResult),
                new Label("active", group.Key.Active ? "true" : "false"));
        }
    }

    private static void AppendCounter(StringBuilder builder, string name, string help, long value)
    {
        AppendHelpAndType(builder, name, help, "counter");
        AppendSample(builder, name, value);
    }

    private static void AppendGauge(StringBuilder builder, string name, string help, long value)
    {
        AppendHelpAndType(builder, name, help, "gauge");
        AppendSample(builder, name, value);
    }

    private static void AppendLabeledCounter(
        StringBuilder builder,
        string name,
        string? help,
        long value,
        params Label[] labels)
    {
        if (help is not null)
        {
            AppendHelpAndType(builder, name, help, "counter");
        }

        AppendSample(builder, name, value, labels);
    }

    private static void AppendHelpAndType(StringBuilder builder, string name, string help, string type)
    {
        builder.Append("# HELP ").Append(name).Append(' ').Append(help).Append('\n');
        builder.Append("# TYPE ").Append(name).Append(' ').Append(type).Append('\n');
    }

    private static void AppendSample(StringBuilder builder, string name, long value, params Label[] labels)
    {
        builder.Append(name);
        if (labels.Length > 0)
        {
            builder.Append('{');
            for (var index = 0; index < labels.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                builder
                    .Append(labels[index].Name)
                    .Append("=\"")
                    .Append(EscapeLabelValue(ProxyMetricLabelPolicy.NormalizeValue(labels[index].Value)))
                    .Append('"');
            }

            builder.Append('}');
        }

        builder.Append(' ')
            .Append(value.ToString(CultureInfo.InvariantCulture))
            .Append('\n');
    }

    private static string EscapeLabelValue(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private readonly record struct Label(string Name, string Value);
}
