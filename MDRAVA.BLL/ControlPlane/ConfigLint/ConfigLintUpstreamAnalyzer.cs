using MDRAVA.BLL.Configuration;
using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.ControlPlane.ConfigLint;

public static class ConfigLintUpstreamAnalyzer
{
    public static IReadOnlyList<ConfigLintFinding> Analyze(
        ProxyConfigLintConfigurationSnapshot snapshot,
        ProxyConfigLintRoute route,
        string routePath,
        string? sourceName)
    {
        List<ConfigLintFinding> findings = [];
        foreach (var upstream in route.Upstreams)
        {
            AddUpstreamFindings(snapshot, route, routePath, upstream, sourceName, findings);
        }

        return findings;
    }

    private static void AddUpstreamFindings(
        ProxyConfigLintConfigurationSnapshot snapshot,
        ProxyConfigLintRoute route,
        string routePath,
        ProxyConfigLintUpstream upstream,
        string? sourceName,
        List<ConfigLintFinding> findings)
    {
        var upstreamPath = $"{routePath}.upstreams[{upstream.Name}]";
        if (string.Equals(upstream.Protocol, RuntimeUpstreamProtocol.Http2, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(upstream.Scheme, "https", StringComparison.OrdinalIgnoreCase))
        {
            findings.Add(Error("upstream_http2_without_https", $"Upstream '{upstream.Name}' uses HTTP/2 without HTTPS.", sourceName, upstreamPath, "Set scheme to https or use upstream protocol http1."));
        }

        if (string.Equals(upstream.Protocol, RuntimeUpstreamProtocol.Http3, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.Equals(upstream.Scheme, "https", StringComparison.OrdinalIgnoreCase))
            {
                findings.Add(Error("upstream_http3_without_https", $"Upstream '{upstream.Name}' uses HTTP/3 without HTTPS.", sourceName, upstreamPath, "Set scheme to https because h3c is not supported."));
            }

            if (!snapshot.Http3QuicConnectionSupported)
            {
                findings.Add(Warning("upstream_http3_runtime_unavailable", $"Upstream '{upstream.Name}' uses HTTP/3 but this runtime does not report QUIC client support.", sourceName, upstreamPath, "Use HTTP/1.1 or HTTP/2 for this upstream on runtimes without QUIC client support."));
            }

            if (route.RetryEnabled
                && route.RetryMethods.Any(static method => !ProxyRequestMethodPolicy.IsSafeReadMethod(method)))
            {
                findings.Add(Warning("upstream_http3_retry_body_safety", $"Route '{route.Name}' combines HTTP/3 upstreams with retry methods beyond GET/HEAD.", sourceName, upstreamPath, "Keep HTTP/3 upstream retries limited to methods without request bodies unless replay is explicitly safe."));
            }
        }

        if (string.Equals(upstream.Scheme, "https", StringComparison.OrdinalIgnoreCase)
            && !upstream.TlsValidateCertificate)
        {
            findings.Add(Warning("unsafe_upstream_tls_validation_disabled", $"Upstream '{upstream.Name}' disables platform TLS certificate validation.", sourceName, upstreamPath, "Use this only for local testing and restore certificate validation before production use."));
        }
    }

    private static ConfigLintFinding Warning(
        string code,
        string message,
        string? source,
        string? path,
        string? suggestedFix)
    {
        return new ConfigLintFinding("warning", code, message, source, path, suggestedFix);
    }

    private static ConfigLintFinding Error(
        string code,
        string message,
        string? source,
        string? path,
        string? suggestedFix)
    {
        return new ConfigLintFinding("error", code, message, source, path, suggestedFix);
    }
}
