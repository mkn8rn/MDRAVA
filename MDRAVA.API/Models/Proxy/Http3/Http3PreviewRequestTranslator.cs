using System.Globalization;
using MDRAVA.API.Proxy.Protocol;

namespace MDRAVA.API.Proxy.Http3;

public static class Http3PreviewRequestTranslator
{
    private static readonly HashSet<string> ForbiddenHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "connection",
        "upgrade",
        "keep-alive",
        "proxy-connection",
        "transfer-encoding",
        "te"
    };

    public static bool TryBuildRequest(
        IReadOnlyList<Http1HeaderField> headers,
        RuntimeListener listener,
        out Http1RequestHead requestHead,
        out string rejectionReason,
        long bodyLength = 0)
    {
        requestHead = null!;
        rejectionReason = "invalid_headers";
        Dictionary<string, string> pseudo = new(StringComparer.Ordinal);
        List<Http1HeaderField> regularHeaders = [];
        var regularHeaderSeen = false;

        foreach (var header in headers)
        {
            if (header.Name.Length == 0)
            {
                rejectionReason = "empty_header_name";
                return false;
            }

            if (header.Name.Any(static character => char.IsAsciiLetterUpper(character)))
            {
                rejectionReason = "uppercase_header_name";
                return false;
            }

            if (header.Name[0] == ':')
            {
                if (regularHeaderSeen
                    || pseudo.ContainsKey(header.Name)
                    || !IsAllowedPseudoHeader(header.Name))
                {
                    rejectionReason = "invalid_pseudo_header";
                    return false;
                }

                pseudo[header.Name] = header.Value;
                continue;
            }

            regularHeaderSeen = true;
            if (ForbiddenHeaders.Contains(header.Name))
            {
                rejectionReason = "forbidden_header";
                return false;
            }

            regularHeaders.Add(header);
        }

        if (!pseudo.TryGetValue(":method", out var method)
            || !pseudo.TryGetValue(":scheme", out var scheme)
            || !pseudo.TryGetValue(":authority", out var authority)
            || !pseudo.TryGetValue(":path", out var target))
        {
            rejectionReason = "missing_pseudo_header";
            return false;
        }

        if (!string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase)
            || listener.Transport != RuntimeListenerTransport.Https)
        {
            rejectionReason = "invalid_scheme";
            return false;
        }

        if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(target) || !target.StartsWith('/'))
        {
            rejectionReason = "invalid_target";
            return false;
        }

        var hostHeader = regularHeaders.FirstOrDefault(static header => string.Equals(header.Name, "host", StringComparison.OrdinalIgnoreCase));
        if (hostHeader is not null && !string.Equals(hostHeader.Value, authority, StringComparison.OrdinalIgnoreCase))
        {
            rejectionReason = "authority_host_mismatch";
            return false;
        }

        if (!TryValidateContentLength(regularHeaders, bodyLength, out rejectionReason))
        {
            return false;
        }

        regularHeaders.RemoveAll(static header => string.Equals(header.Name, "host", StringComparison.OrdinalIgnoreCase));
        var path = target.Split('?', 2)[0];
        var framing = Http1RequestFraming.FromContentLength(bodyLength);
        regularHeaders.Insert(0, new Http1HeaderField("Host", authority));
        requestHead = new Http1RequestHead(
            method,
            target,
            path,
            "HTTP/3",
            authority,
            framing,
            regularHeaders);
        return true;
    }

    public static bool IsSupportedPreviewMethod(string method, out string rejectionReason)
    {
        if (string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "PATCH", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase))
        {
            rejectionReason = "";
            return true;
        }

        rejectionReason = string.Equals(method, "CONNECT", StringComparison.OrdinalIgnoreCase)
            ? "connect_unsupported"
            : "method_unsupported";
        return false;
    }

    private static bool IsAllowedPseudoHeader(string name)
    {
        return name is ":method" or ":scheme" or ":authority" or ":path";
    }

    private static bool TryValidateContentLength(
        IReadOnlyList<Http1HeaderField> headers,
        long bodyLength,
        out string rejectionReason)
    {
        rejectionReason = "";
        long? declared = null;
        foreach (var header in headers)
        {
            if (!string.Equals(header.Name, "content-length", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (declared.HasValue
                || !long.TryParse(header.Value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed)
                || parsed < 0)
            {
                rejectionReason = "invalid_content_length";
                return false;
            }

            declared = parsed;
        }

        if (declared.HasValue && declared.Value != bodyLength)
        {
            rejectionReason = "invalid_content_length";
            return false;
        }

        return true;
    }
}
