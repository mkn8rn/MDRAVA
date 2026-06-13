using MDRAVA.BLL.ControlPlane.RuntimeGuards;

namespace MDRAVA.BLL.ControlPlane.RouteDiagnostics;

public static partial class ProxyRouteDiagnosticsRequestReader
{
    private const int MaxInputLength = 4096;

    public static ProxyRouteDiagnosticsRequestDecision Read(
        RouteMatchDryRunRequest? request,
        DateTimeOffset evaluatedAtUtc,
        IProxyClientAddressSyntaxPolicy clientAddressSyntaxPolicy)
    {
        if (request is null)
        {
            return ProxyRouteDiagnosticsRequestDecision.Rejected(
                Failure(evaluatedAtUtc, "missing_request", "A dry-run request body is required."));
        }

        var scheme = NormalizeScheme(request.Scheme);
        if (scheme is not "http" and not "https")
        {
            return ProxyRouteDiagnosticsRequestDecision.Rejected(
                Failure(evaluatedAtUtc, "invalid_scheme", "Scheme must be 'http' or 'https'."));
        }

        var protocol = NormalizeProtocol(request.Protocol);
        if (protocol is not null and not "http1" and not "http2" and not "http3")
        {
            return ProxyRouteDiagnosticsRequestDecision.Rejected(
                Failure(evaluatedAtUtc, "invalid_protocol", "Protocol must be 'http1', 'http2', or 'http3' when supplied."));
        }

        if (protocol == "http3" && scheme != "https")
        {
            return ProxyRouteDiagnosticsRequestDecision.Rejected(
                Failure(evaluatedAtUtc, "invalid_protocol", "HTTP/3 dry-runs must use the https scheme."));
        }

        if (string.IsNullOrWhiteSpace(request.Host) || request.Host.Length > 253 || ContainsControl(request.Host))
        {
            return ProxyRouteDiagnosticsRequestDecision.Rejected(
                Failure(evaluatedAtUtc, "invalid_host", "Host is required and must not contain control characters."));
        }

        if (request.Port is < 1 or > 65535)
        {
            return ProxyRouteDiagnosticsRequestDecision.Rejected(
                Failure(evaluatedAtUtc, "invalid_port", "Port must be between 1 and 65535 when supplied."));
        }

        var method = NormalizeMethod(request.Method);
        if (method.Length is 0 or > 32 || ContainsControl(method) || method.Any(static character => char.IsWhiteSpace(character)))
        {
            return ProxyRouteDiagnosticsRequestDecision.Rejected(
                Failure(evaluatedAtUtc, "invalid_method", "Method must be a non-empty HTTP token."));
        }

        var path = NormalizePath(request.Path);
        if (path.Length is 0 or > MaxInputLength || !path.StartsWith('/'))
        {
            return ProxyRouteDiagnosticsRequestDecision.Rejected(
                Failure(evaluatedAtUtc, "invalid_path", "Path must start with '/' and stay within the dry-run size limit."));
        }

        if (ContainsControl(path))
        {
            return ProxyRouteDiagnosticsRequestDecision.Rejected(
                Failure(evaluatedAtUtc, "invalid_path", "Path must not contain control characters."));
        }

        var query = NormalizeQuery(request.Query);
        if (query.Length > MaxInputLength || ContainsControl(query))
        {
            return ProxyRouteDiagnosticsRequestDecision.Rejected(
                Failure(evaluatedAtUtc, "invalid_query", "Query must stay within the dry-run size limit and must not contain control characters."));
        }

        if (!string.IsNullOrWhiteSpace(request.ClientIp) && !clientAddressSyntaxPolicy.IsIpLiteral(request.ClientIp))
        {
            return ProxyRouteDiagnosticsRequestDecision.Rejected(
                Failure(evaluatedAtUtc, "invalid_client_ip", "ClientIp must be an IPv4 or IPv6 literal when supplied."));
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

        return ProxyRouteDiagnosticsRequestDecision.Accepted(
            new ProxyRouteDiagnosticsRequestInput(
                scheme,
                protocol,
                request.ListenerName,
                request.Port,
                target,
                path,
                requestHead,
                IsUpgrade(headers),
                findings));
    }

    private static RouteMatchDryRunResult.FailedResult Failure(DateTimeOffset evaluatedAtUtc, string reason, string message)
    {
        return RouteMatchDryRunResult.Failed(evaluatedAtUtc, reason, message);
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
