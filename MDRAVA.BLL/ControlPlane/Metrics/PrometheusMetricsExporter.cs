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
        var configReloads = proxy.ConfigReloads;
        var adminAuth = proxy.AdminAuth;
        var acmeRenewals = proxy.AcmeRenewals;
        var clientConnections = proxy.ClientConnections;
        var rejections = proxy.Rejections;
        var builder = new StringBuilder();

        AppendCounter(builder, "mdrava_client_connections_accepted_total", "Accepted downstream client connections.", clientConnections.Accepted);
        AppendGauge(builder, "mdrava_client_connections_active", "Currently active downstream client connections.", clientConnections.Active);
        AppendLabeledCounter(builder, "mdrava_client_connections_rejected_total", "Rejected downstream client connections by bounded reason.", rejections.ClientConnectionAdmissionRejections, new Label("reason", "admission_limit"));

        AppendCounter(builder, "mdrava_requests_total", "HTTP requests received by the dataplane.", proxy.TotalRequests);
        AppendClientProtocolMetrics(builder, input, proxy);
        AppendRouteRequestCounters(builder, input.IncludePerRouteLabels, proxy.RequestsByRoute);
        AppendRequestRejectionCounters(builder, proxy);
        AppendUpstreamAndResilienceMetrics(builder, input, proxy, health);

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

        AppendLabeledCounter(builder, "mdrava_config_reloads_total", "Configuration reloads by result.", configReloads.Successes, new Label("result", "success"));
        AppendLabeledCounter(builder, "mdrava_config_reloads_total", null, configReloads.Failures, new Label("result", "failure"));
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

        var listeners = proxy.Listeners;
        AppendLabeledCounter(builder, "mdrava_listener_reloads_total", "Proxy listener reload attempts by result.", listeners.ReloadSuccesses, new Label("result", "success"));
        AppendLabeledCounter(builder, "mdrava_listener_reloads_total", null, listeners.ReloadFailures, new Label("result", "failure"));
        AppendCounter(builder, "mdrava_listener_reload_attempts_total", "Proxy listener reload attempts.", listeners.ReloadAttempts);
        AppendLabeledCounter(builder, "mdrava_listener_reload_changes_total", "Proxy listener reload changes by bounded action.", listeners.ReloadAdded, new Label("action", "added"));
        AppendLabeledCounter(builder, "mdrava_listener_reload_changes_total", null, listeners.ReloadRemoved, new Label("action", "removed"));
        AppendLabeledCounter(builder, "mdrava_listener_reload_changes_total", null, listeners.ReloadChanged, new Label("action", "changed"));
        AppendLabeledCounter(builder, "mdrava_listener_reload_changes_total", null, listeners.ReloadUnchanged, new Label("action", "unchanged"));
        AppendCounter(builder, "mdrava_listener_start_failures_total", "Proxy listener start failures.", listeners.StartFailures);
        AppendCounter(builder, "mdrava_listener_drains_total", "Proxy listener drains after reload removal or replacement.", listeners.Drains);
        AppendGauge(builder, "mdrava_listeners_active", "Currently active proxy listeners.", listeners.ActiveListeners);
        AppendLabeledCounter(builder, "mdrava_admin_auth_total", "Admin authentication attempts by result.", adminAuth.Successes, new Label("result", "success"));
        AppendLabeledCounter(builder, "mdrava_admin_auth_total", null, adminAuth.Failures, new Label("result", "failure"));
        AppendLabeledCounter(builder, "mdrava_acme_renewals_total", "ACME renewal attempts by result.", acmeRenewals.Attempts, new Label("result", "attempt"));
        AppendLabeledCounter(builder, "mdrava_acme_renewals_total", null, acmeRenewals.Successes, new Label("result", "success"));
        AppendLabeledCounter(builder, "mdrava_acme_renewals_total", null, acmeRenewals.Failures, new Label("result", "failure"));
        AppendAcmeCertificateStatus(builder, acme);

        return builder.ToString();
    }
}
