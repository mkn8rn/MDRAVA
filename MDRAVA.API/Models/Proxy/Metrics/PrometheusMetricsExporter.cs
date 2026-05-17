using System.Globalization;
using System.Text;
using MDRAVA.API.Proxy.Acme;
using MDRAVA.API.Proxy.Caching;
using MDRAVA.API.Proxy.Configuration.Runtime;
using MDRAVA.API.Proxy.Health;

namespace MDRAVA.API.Proxy.Metrics;

public sealed class PrometheusMetricsExporter
{
    public const string ContentType = "text/plain; version=0.0.4; charset=utf-8";
    private const int MaxLabelLength = 96;

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
        var cache = _cacheStore.Snapshot(snapshot);
        var health = _healthStore.Snapshot(snapshot);
        var acme = _acmeStatusStore.Snapshot();
        var builder = new StringBuilder();

        AppendCounter(builder, "mdrava_client_connections_accepted_total", "Accepted downstream client connections.", proxy.AcceptedConnections);
        AppendGauge(builder, "mdrava_client_connections_active", "Currently active downstream client connections.", proxy.ActiveConnections);
        AppendLabeledCounter(builder, "mdrava_client_connections_rejected_total", "Rejected downstream client connections by bounded reason.", proxy.ConnectionAdmissionRejections, new Label("reason", "admission_limit"));

        AppendCounter(builder, "mdrava_requests_total", "HTTP requests received by the dataplane.", proxy.TotalRequests);
        AppendRouteRequestCounters(builder, snapshot.Metrics, proxy.RequestsByRoute);
        AppendRequestRejectionCounters(builder, proxy);

        AppendCounter(builder, "mdrava_upstream_request_attempts_total", "Selected upstream request attempts.", proxy.UpstreamSelections);
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", "Upstream failures by bounded reason.", proxy.UpstreamConnectFailures, new Label("reason", "connect_failure"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, proxy.UpstreamConnectTimeouts, new Label("reason", "connect_timeout"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, proxy.UpstreamResponseHeadTimeouts, new Label("reason", "response_head_timeout"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, proxy.UpstreamResponseBodyTimeouts, new Label("reason", "response_body_timeout"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, proxy.UpstreamMalformedResponses, new Label("reason", "malformed_response"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, proxy.UpstreamPrematureDisconnects, new Label("reason", "premature_disconnect"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, proxy.NoHealthyUpstreamFailures, new Label("reason", "no_healthy_upstream"));
        AppendLabeledCounter(builder, "mdrava_upstream_failures_total", null, proxy.UpstreamRequestFailures, new Label("reason", "request_failure"));

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

    private static void AppendUpstreamHealth(
        StringBuilder builder,
        RuntimeMetricsOptions options,
        IReadOnlyList<UpstreamHealthRecord> health)
    {
        if (!options.IncludePerUpstreamLabels)
        {
            return;
        }

        AppendHelpAndType(builder, "mdrava_upstream_health_up", "Current upstream health status, 1 for healthy and 0 otherwise.", "gauge");
        foreach (var upstream in health)
        {
            var value = upstream.State == UpstreamHealthState.Healthy ? 1 : 0;
            AppendSample(
                builder,
                "mdrava_upstream_health_up",
                value,
                new Label("route", upstream.RouteName),
                new Label("upstream", upstream.UpstreamName),
                new Label("scheme", upstream.Scheme),
                new Label("state", upstream.State.ToString()));
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
                    .Append(EscapeLabelValue(SafeLabelValue(labels[index].Value)))
                    .Append('"');
            }

            builder.Append('}');
        }

        builder.Append(' ')
            .Append(value.ToString(CultureInfo.InvariantCulture))
            .Append('\n');
    }

    private static string SafeLabelValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "none";
        }

        Span<char> buffer = stackalloc char[Math.Min(value.Length, MaxLabelLength)];
        var index = 0;
        foreach (var character in value.Trim())
        {
            if (index >= buffer.Length)
            {
                break;
            }

            buffer[index++] = char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'
                ? character
                : '_';
        }

        return index == 0 ? "none" : new string(buffer[..index]);
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
