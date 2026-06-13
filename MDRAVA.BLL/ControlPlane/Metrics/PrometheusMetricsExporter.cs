using System.Text;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class PrometheusMetricsExporter
{
    public const string ContentType = "text/plain; version=0.0.4; charset=utf-8";

    public string Export(ProxyMetricsExportInput input)
    {
        var proxy = input.Metrics;
        var cache = input.CacheStatus;
        var health = input.UpstreamHealth;
        var acme = input.AcmeCertificates;
        var builder = new StringBuilder();

        AppendCounter(builder, "mdrava_client_connections_accepted_total", "Accepted downstream client connections.", proxy.AcceptedConnections);
        AppendGauge(builder, "mdrava_client_connections_active", "Currently active downstream client connections.", proxy.ActiveConnections);
        AppendLabeledCounter(builder, "mdrava_client_connections_rejected_total", "Rejected downstream client connections by bounded reason.", proxy.ConnectionAdmissionRejections, new Label("reason", "admission_limit"));

        AppendCounter(builder, "mdrava_requests_total", "HTTP requests received by the dataplane.", proxy.TotalRequests);
        AppendClientProtocolMetrics(builder, input, proxy);
        AppendRouteRequestCounters(builder, input.IncludePerRouteLabels, proxy.RequestsByRoute);
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
        AppendGauge(builder, "mdrava_upstream_http3_multiplexing_enabled", "Whether upstream HTTP/3 multiplexing is enabled.", proxy.UpstreamHttp3Requests > 0 || input.UpstreamHttp3MultiplexingConfigured ? 1 : 0);
        AppendGauge(builder, "mdrava_upstream_http3_connections_active", "Active upstream HTTP/3 QUIC connections.", proxy.ActiveUpstreamHttp3Connections);
        AppendGauge(builder, "mdrava_upstream_http3_streams_active", "Active upstream HTTP/3 streams.", proxy.ActiveUpstreamHttp3Streams);
        AppendUpstreamSelectionCounters(builder, input.IncludePerUpstreamLabels, proxy.UpstreamSelectionsByUpstream);
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
        AppendUpstreamHealth(builder, input.IncludePerUpstreamLabels, health);

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
}
