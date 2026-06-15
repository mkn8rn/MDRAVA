using MDRAVA.BLL.ControlPlane.Acme;
using MDRAVA.BLL.ControlPlane.HealthChecks;
using MDRAVA.BLL.ControlPlane.Status;
using System.Globalization;
using System.Text;

namespace MDRAVA.BLL.ControlPlane.Metrics;

public sealed partial class PrometheusMetricsExporter
{
    private static void AppendUpstreamSelectionCounters(
        StringBuilder builder,
        bool includePerUpstreamLabels,
        IReadOnlyList<ProxyUpstreamSelectionSnapshot> selections)
    {
        if (!includePerUpstreamLabels || selections.Count == 0)
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
        bool includePerUpstreamLabels,
        IReadOnlyList<ProxyUpstreamStatus> health)
    {
        if (!includePerUpstreamLabels)
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
                new Label("state", UpstreamHealthStateText.FromState(upstream.HealthState)));
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
