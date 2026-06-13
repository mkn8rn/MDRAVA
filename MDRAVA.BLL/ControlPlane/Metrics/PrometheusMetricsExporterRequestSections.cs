using System.Text;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class PrometheusMetricsExporter
{
    private static void AppendRouteRequestCounters(
        StringBuilder builder,
        bool includePerRouteLabels,
        IReadOnlyList<ProxyRequestSeriesSnapshot> requests)
    {
        AppendHelpAndType(builder, "mdrava_route_requests_total", "Completed requests by bounded route/action/status labels.", "counter");
        IEnumerable<ProxyRequestSeriesSnapshot> series = includePerRouteLabels
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
            var labels = includePerRouteLabels
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
        var rejections = proxy.Rejections;
        AppendLabeledCounter(builder, "mdrava_request_rejections_total", "Request rejections by bounded reason.", rejections.RateLimitedRequests, new Label("reason", "rate_limited"));
        AppendLabeledCounter(builder, "mdrava_request_rejections_total", null, rejections.RateLimitedUpgrades, new Label("reason", "upgrade_rate_limited"));
        AppendLabeledCounter(builder, "mdrava_request_rejections_total", null, rejections.RequestBodySizeRejections, new Label("reason", "body_too_large"));
        AppendLabeledCounter(builder, "mdrava_request_rejections_total", null, rejections.ParserLimitRejections, new Label("reason", "parser_limit"));
        AppendLabeledCounter(builder, "mdrava_request_rejections_total", null, rejections.MalformedRequests, new Label("reason", "malformed"));
        AppendLabeledCounter(builder, "mdrava_request_rejections_total", null, rejections.UnsupportedRequestFraming, new Label("reason", "unsupported_framing"));
    }
}
