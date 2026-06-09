using System.Globalization;

namespace MDRAVA.API.Proxy.Http3;

public static class Http3RequestTranslator
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
        bool bodyMayFollow = true)
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
            if (!IsValidHeaderName(header.Name))
            {
                rejectionReason = "invalid_header_name";
                return false;
            }

            if (ForbiddenHeaders.Contains(header.Name))
            {
                rejectionReason = "forbidden_header";
                return false;
            }

            regularHeaders.Add(header);
        }

        if (!pseudo.TryGetValue(":method", out var method))
        {
            rejectionReason = "missing_pseudo_header";
            return false;
        }

        if (pseudo.ContainsKey(":protocol"))
        {
            rejectionReason = "extended_connect_unsupported";
            return false;
        }

        if (string.Equals(method, "CONNECT", StringComparison.OrdinalIgnoreCase))
        {
            return TryBuildConnectRequest(pseudo, regularHeaders, listener, method, out requestHead, out rejectionReason);
        }

        if (!pseudo.TryGetValue(":scheme", out var scheme)
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

        if (!IsValidMethod(method))
        {
            rejectionReason = "invalid_method";
            return false;
        }

        if (!IsValidAuthority(authority) || !IsValidTarget(target))
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

        if (!TryGetRequestFraming(regularHeaders, method, bodyMayFollow, out var framing, out rejectionReason))
        {
            return false;
        }

        regularHeaders.RemoveAll(static header => string.Equals(header.Name, "host", StringComparison.OrdinalIgnoreCase));
        var path = target.Split('?', 2)[0];
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

    public static bool IsSupportedMethod(string method, out string rejectionReason)
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
        return name is ":method" or ":scheme" or ":authority" or ":path" or ":protocol";
    }

    private static bool TryBuildConnectRequest(
        IReadOnlyDictionary<string, string> pseudo,
        List<Http1HeaderField> regularHeaders,
        RuntimeListener listener,
        string method,
        out Http1RequestHead requestHead,
        out string rejectionReason)
    {
        requestHead = null!;
        rejectionReason = "";
        if (pseudo.ContainsKey(":scheme") || pseudo.ContainsKey(":path"))
        {
            rejectionReason = "malformed_connect";
            return false;
        }

        if (!pseudo.TryGetValue(":authority", out var authority)
            || !IsValidConnectAuthority(authority))
        {
            rejectionReason = "invalid_connect_target";
            return false;
        }

        if (listener.Transport != RuntimeListenerTransport.Https)
        {
            rejectionReason = "invalid_scheme";
            return false;
        }

        var hostHeader = regularHeaders.FirstOrDefault(static header => string.Equals(header.Name, "host", StringComparison.OrdinalIgnoreCase));
        if (hostHeader is not null && !string.Equals(hostHeader.Value, authority, StringComparison.OrdinalIgnoreCase))
        {
            rejectionReason = "authority_host_mismatch";
            return false;
        }

        if (!TryGetRequestFraming(regularHeaders, method, bodyMayFollow: false, out var framing, out rejectionReason))
        {
            return false;
        }

        if (framing.Kind != Http1BodyKind.None)
        {
            rejectionReason = "connect_body_unsupported";
            return false;
        }

        regularHeaders.RemoveAll(static header => string.Equals(header.Name, "host", StringComparison.OrdinalIgnoreCase));
        regularHeaders.Insert(0, new Http1HeaderField("Host", authority));
        requestHead = new Http1RequestHead(
            method,
            authority,
            authority,
            "HTTP/3",
            authority,
            Http1RequestFraming.None,
            regularHeaders);
        return true;
    }

    private static bool IsValidMethod(string method)
    {
        return !string.IsNullOrWhiteSpace(method)
            && method.All(static character => character is '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+'
                or '-' or '.' or '^' or '_' or '`' or '|' or '~'
                || character is >= 'A' and <= 'Z'
                || character is >= 'a' and <= 'z'
                || character is >= '0' and <= '9');
    }

    private static bool IsValidAuthority(string authority)
    {
        return !string.IsNullOrWhiteSpace(authority)
            && authority.All(static character => character > 0x20
                && character != 0x7f
                && character is not '/' and not '\\' and not '?' and not '#' and not '@');
    }

    private static bool IsValidConnectAuthority(string authority)
    {
        if (!IsValidAuthority(authority))
        {
            return false;
        }

        var portSeparator = authority.LastIndexOf(':');
        if (portSeparator <= 0 || portSeparator == authority.Length - 1)
        {
            return false;
        }

        if (authority[0] == '[')
        {
            var bracket = authority.IndexOf(']');
            if (bracket <= 1 || bracket + 1 != portSeparator)
            {
                return false;
            }
        }
        else if (authority.IndexOf(':') != portSeparator)
        {
            return false;
        }

        return int.TryParse(authority[(portSeparator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var port)
            && port is >= 1 and <= 65535;
    }

    private static bool IsValidTarget(string target)
    {
        return !string.IsNullOrWhiteSpace(target)
            && target.StartsWith("/", StringComparison.Ordinal)
            && target.All(static character => character > 0x20 && character != 0x7f && character != '#');
    }

    private static bool IsValidHeaderName(string name)
    {
        return !string.IsNullOrWhiteSpace(name)
            && name.All(static character => character is '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+'
                or '-' or '.' or '^' or '_' or '`' or '|' or '~'
                || character is >= '0' and <= '9'
                || character is >= 'a' and <= 'z');
    }

    private static bool TryGetRequestFraming(
        IReadOnlyList<Http1HeaderField> headers,
        string method,
        bool bodyMayFollow,
        out Http1RequestFraming framing,
        out string rejectionReason)
    {
        framing = Http1RequestFraming.None;
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

        if (declared.HasValue)
        {
            framing = Http1RequestFraming.FromContentLength(declared.Value);
            return true;
        }

        if (bodyMayFollow && MayCarryBody(method))
        {
            framing = Http1RequestFraming.Chunked;
        }

        return true;
    }

    private static bool MayCarryBody(string method)
    {
        return string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "PATCH", StringComparison.OrdinalIgnoreCase)
            || string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase);
    }
}
