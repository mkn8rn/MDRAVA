using MDRAVA.BLL.ControlPlane.RuntimeGuards;
using MDRAVA.BLL.Http;

namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public sealed record ProxyRouteDiagnosticsRequestInput(
    string Scheme,
    string? Protocol,
    string? ListenerName,
    int? Port,
    string Target,
    string Path,
    ProxyRouteDiagnosticsRequestHead RequestHead,
    bool IsUpgradeRequest,
    List<RouteMatchDryRunFinding> Findings);

public static class ProxyRouteDiagnosticsRequestReader
{
    private const int MaxInputLength = 4096;
    private static readonly HashSet<string> SensitiveHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Proxy-Authorization",
        "Set-Cookie",
        "X-MDRAVA-Admin-Key"
    };

    public static bool TryRead(
        RouteMatchDryRunRequest? request,
        DateTimeOffset evaluatedAtUtc,
        IProxyClientAddressSyntaxPolicy clientAddressSyntaxPolicy,
        out ProxyRouteDiagnosticsRequestInput? input,
        out RouteMatchDryRunResult? failure)
    {
        input = null;
        failure = Failure(evaluatedAtUtc, "invalid_input", "The dry-run request is invalid.");
        if (request is null)
        {
            failure = Failure(evaluatedAtUtc, "missing_request", "A dry-run request body is required.");
            return false;
        }

        var scheme = NormalizeScheme(request.Scheme);
        if (scheme is not "http" and not "https")
        {
            failure = Failure(evaluatedAtUtc, "invalid_scheme", "Scheme must be 'http' or 'https'.");
            return false;
        }

        var protocol = NormalizeProtocol(request.Protocol);
        if (protocol is not null and not "http1" and not "http2" and not "http3")
        {
            failure = Failure(evaluatedAtUtc, "invalid_protocol", "Protocol must be 'http1', 'http2', or 'http3' when supplied.");
            return false;
        }

        if (protocol == "http3" && scheme != "https")
        {
            failure = Failure(evaluatedAtUtc, "invalid_protocol", "HTTP/3 dry-runs must use the https scheme.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Host) || request.Host.Length > 253 || ContainsControl(request.Host))
        {
            failure = Failure(evaluatedAtUtc, "invalid_host", "Host is required and must not contain control characters.");
            return false;
        }

        if (request.Port is < 1 or > 65535)
        {
            failure = Failure(evaluatedAtUtc, "invalid_port", "Port must be between 1 and 65535 when supplied.");
            return false;
        }

        var method = NormalizeMethod(request.Method);
        if (method.Length is 0 or > 32 || ContainsControl(method) || method.Any(static character => char.IsWhiteSpace(character)))
        {
            failure = Failure(evaluatedAtUtc, "invalid_method", "Method must be a non-empty HTTP token.");
            return false;
        }

        var path = NormalizePath(request.Path);
        if (path.Length is 0 or > MaxInputLength || !path.StartsWith('/'))
        {
            failure = Failure(evaluatedAtUtc, "invalid_path", "Path must start with '/' and stay within the dry-run size limit.");
            return false;
        }

        if (ContainsControl(path))
        {
            failure = Failure(evaluatedAtUtc, "invalid_path", "Path must not contain control characters.");
            return false;
        }

        var query = NormalizeQuery(request.Query);
        if (query.Length > MaxInputLength || ContainsControl(query))
        {
            failure = Failure(evaluatedAtUtc, "invalid_query", "Query must stay within the dry-run size limit and must not contain control characters.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.ClientIp) && !clientAddressSyntaxPolicy.IsIpLiteral(request.ClientIp))
        {
            failure = Failure(evaluatedAtUtc, "invalid_client_ip", "ClientIp must be an IPv4 or IPv6 literal when supplied.");
            return false;
        }

        var findings = new List<RouteMatchDryRunFinding>();
        var target = path + query;
        var headers = BuildHeaders(request, findings);
        var requestHead = new ProxyRouteDiagnosticsRequestHead(
            method,
            target,
            path,
            "HTTP/1.1",
            request.Host.Trim(),
            ResolveFraming(headers),
            headers);

        input = new ProxyRouteDiagnosticsRequestInput(
            scheme,
            protocol,
            request.ListenerName,
            request.Port,
            target,
            path,
            requestHead,
            IsUpgrade(headers),
            findings);
        failure = null;
        return true;
    }

    private static RouteMatchDryRunResult Failure(DateTimeOffset evaluatedAtUtc, string reason, string message)
    {
        return new RouteMatchDryRunResult(
            false,
            evaluatedAtUtc,
            reason,
            null,
            null,
            null,
            null,
            null,
            false,
            null,
            null,
            null,
            null,
            ProxyRouteDiagnosticsPolicyExplainer.Disabled(reason),
            ProxyRouteDiagnosticsPolicyExplainer.Disabled(reason),
            ProxyRouteDiagnosticsPolicyExplainer.Disabled(reason),
            [new RouteMatchDryRunFinding("error", reason, message)]);
    }

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

    private static string NormalizeScheme(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "http" : value.Trim().ToLowerInvariant();
    }

    private static string NormalizeMethod(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "GET" : value.Trim().ToUpperInvariant();
    }

    private static string NormalizePath(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "/" : value.Trim();
    }

    private static string NormalizeQuery(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var query = value.Trim();
        return query.StartsWith('?') ? query : "?" + query;
    }

    private static string? NormalizeProtocol(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
    }

    private static bool ContainsControl(string value)
    {
        return value.Any(char.IsControl);
    }
}
