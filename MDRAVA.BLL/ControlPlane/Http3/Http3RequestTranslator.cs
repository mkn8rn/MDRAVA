using MDRAVA.BLL.ControlPlane.Headers;
using MDRAVA.BLL.ControlPlane.Http1;
using MDRAVA.BLL.Http;
using System.Globalization;
using MDRAVA.BLL.Configuration;

namespace MDRAVA.BLL.ControlPlane.Http3;

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

    public static Http3RequestTranslationResult BuildRequest(
        IReadOnlyList<ProxyHeaderField> headers,
        RuntimeListener listener,
        bool bodyMayFollow = true)
    {
        Dictionary<string, string> pseudo = new(StringComparer.Ordinal);
        List<ProxyHeaderField> regularHeaders = [];
        var regularHeaderSeen = false;

        foreach (var header in headers)
        {
            if (header.Name.Length == 0)
            {
                return Http3RequestTranslationResult.Rejected("empty_header_name");
            }

            if (header.Name.Any(static character => char.IsAsciiLetterUpper(character)))
            {
                return Http3RequestTranslationResult.Rejected("uppercase_header_name");
            }

            if (header.Name[0] == ':')
            {
                if (regularHeaderSeen
                    || pseudo.ContainsKey(header.Name)
                    || !IsAllowedPseudoHeader(header.Name))
                {
                    return Http3RequestTranslationResult.Rejected("invalid_pseudo_header");
                }

                pseudo[header.Name] = header.Value;
                continue;
            }

            regularHeaderSeen = true;
            if (!IsValidHeaderName(header.Name))
            {
                return Http3RequestTranslationResult.Rejected("invalid_header_name");
            }

            if (ForbiddenHeaders.Contains(header.Name))
            {
                return Http3RequestTranslationResult.Rejected("forbidden_header");
            }

            regularHeaders.Add(header);
        }

        if (!pseudo.TryGetValue(":method", out var method))
        {
            return Http3RequestTranslationResult.Rejected("missing_pseudo_header");
        }

        if (pseudo.ContainsKey(":protocol"))
        {
            return Http3RequestTranslationResult.Rejected("extended_connect_unsupported");
        }

        if (ProxyRequestMethodPolicy.IsConnectTunnelMethod(method))
        {
            return BuildConnectRequest(pseudo, regularHeaders, listener, method);
        }

        if (!pseudo.TryGetValue(":scheme", out var scheme)
            || !pseudo.TryGetValue(":authority", out var authority)
            || !pseudo.TryGetValue(":path", out var target))
        {
            return Http3RequestTranslationResult.Rejected("missing_pseudo_header");
        }

        if (!string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase)
            || listener.Transport != RuntimeListenerTransport.Https)
        {
            return Http3RequestTranslationResult.Rejected("invalid_scheme");
        }

        if (!ProxyRequestMethodPolicy.IsValidMethodToken(method))
        {
            return Http3RequestTranslationResult.Rejected("invalid_method");
        }

        if (!IsValidAuthority(authority) || !IsValidTarget(target))
        {
            return Http3RequestTranslationResult.Rejected("invalid_target");
        }

        var hostHeader = regularHeaders.FirstOrDefault(static header => string.Equals(header.Name, "host", StringComparison.OrdinalIgnoreCase));
        if (hostHeader is not null && !string.Equals(hostHeader.Value, authority, StringComparison.OrdinalIgnoreCase))
        {
            return Http3RequestTranslationResult.Rejected("authority_host_mismatch");
        }

        if (!TryGetRequestFraming(regularHeaders, method, bodyMayFollow, out var framing, out var rejectionReason))
        {
            return Http3RequestTranslationResult.Rejected(rejectionReason);
        }

        regularHeaders.RemoveAll(static header => string.Equals(header.Name, "host", StringComparison.OrdinalIgnoreCase));
        var path = target.Split('?', 2)[0];
        regularHeaders.Insert(0, new ProxyHeaderField("Host", authority));
        return Http3RequestTranslationResult.Accepted(
            new Http1RequestHead(
                method,
                target,
                path,
                "HTTP/3",
                authority,
                framing,
                regularHeaders));
    }

    private static bool IsAllowedPseudoHeader(string name)
    {
        return name is ":method" or ":scheme" or ":authority" or ":path" or ":protocol";
    }

    private static Http3RequestTranslationResult BuildConnectRequest(
        IReadOnlyDictionary<string, string> pseudo,
        List<ProxyHeaderField> regularHeaders,
        RuntimeListener listener,
        string method)
    {
        if (pseudo.ContainsKey(":scheme") || pseudo.ContainsKey(":path"))
        {
            return Http3RequestTranslationResult.Rejected("malformed_connect");
        }

        if (!pseudo.TryGetValue(":authority", out var authority)
            || !IsValidConnectAuthority(authority))
        {
            return Http3RequestTranslationResult.Rejected("invalid_connect_target");
        }

        if (listener.Transport != RuntimeListenerTransport.Https)
        {
            return Http3RequestTranslationResult.Rejected("invalid_scheme");
        }

        var hostHeader = regularHeaders.FirstOrDefault(static header => string.Equals(header.Name, "host", StringComparison.OrdinalIgnoreCase));
        if (hostHeader is not null && !string.Equals(hostHeader.Value, authority, StringComparison.OrdinalIgnoreCase))
        {
            return Http3RequestTranslationResult.Rejected("authority_host_mismatch");
        }

        if (!TryGetRequestFraming(regularHeaders, method, bodyMayFollow: false, out var framing, out var rejectionReason))
        {
            return Http3RequestTranslationResult.Rejected(rejectionReason);
        }

        if (framing.Kind != Http1BodyKind.None)
        {
            return Http3RequestTranslationResult.Rejected("connect_body_unsupported");
        }

        regularHeaders.RemoveAll(static header => string.Equals(header.Name, "host", StringComparison.OrdinalIgnoreCase));
        regularHeaders.Insert(0, new ProxyHeaderField("Host", authority));
        return Http3RequestTranslationResult.Accepted(
            new Http1RequestHead(
                method,
                authority,
                authority,
                "HTTP/3",
                authority,
                Http1RequestFraming.None,
                regularHeaders));
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
        IReadOnlyList<ProxyHeaderField> headers,
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
