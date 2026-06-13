using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public static partial class ProxyRouteDiagnosticsRequestReader
{
    private static readonly HashSet<string> SensitiveHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Proxy-Authorization",
        "Set-Cookie",
        "X-MDRAVA-Admin-Key"
    };

    private static IReadOnlyList<ProxyHeaderField> BuildHeaders(
        RouteMatchDryRunRequest request,
        List<RouteMatchDryRunFinding> findings)
    {
        List<ProxyHeaderField> headers = [new("Host", request.Host.Trim())];
        foreach (var header in request.Headers.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var name = header.Key?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name) || ContainsControl(name) || name.Contains(':', StringComparison.Ordinal))
            {
                findings.Add(new RouteMatchDryRunFinding("warning", "header_ignored", "A submitted header name was invalid and ignored."));
                continue;
            }

            var value = header.Value ?? "";
            if (ContainsControl(value))
            {
                findings.Add(new RouteMatchDryRunFinding("warning", "header_ignored", "A submitted header value contained control characters and was ignored."));
                continue;
            }

            if (SensitiveHeaderNames.Contains(name))
            {
                findings.Add(new RouteMatchDryRunFinding("info", "sensitive_header_redacted", $"Header '{name}' was considered for policy decisions but is not echoed in diagnostics."));
                value = "redacted";
            }

            headers.Add(new ProxyHeaderField(name, value));
        }

        return headers;
    }

    private static ProxyRouteDiagnosticsRequestFraming ResolveFraming(IReadOnlyList<ProxyHeaderField> headers)
    {
        foreach (var header in headers)
        {
            if (string.Equals(header.Name, "Transfer-Encoding", StringComparison.OrdinalIgnoreCase)
                && header.Value.Contains("chunked", StringComparison.OrdinalIgnoreCase))
            {
                return ProxyRouteDiagnosticsRequestFraming.Chunked;
            }
        }

        foreach (var header in headers)
        {
            if (string.Equals(header.Name, "Content-Length", StringComparison.OrdinalIgnoreCase)
                && long.TryParse(header.Value.Trim(), out var contentLength)
                && contentLength >= 0)
            {
                return ProxyRouteDiagnosticsRequestFraming.FromContentLength(contentLength);
            }
        }

        return ProxyRouteDiagnosticsRequestFraming.None;
    }

    private static bool IsUpgrade(IReadOnlyList<ProxyHeaderField> headers)
    {
        return headers.Any(static header => string.Equals(header.Name, "Upgrade", StringComparison.OrdinalIgnoreCase));
    }
}
